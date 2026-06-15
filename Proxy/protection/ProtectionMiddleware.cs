using System.Diagnostics;
using llm_protector.decorators;
using llm_protector.filters;
using llm_protector.ml;
using llm_protector.static_filter;
using Newtonsoft.Json.Linq;
using Shared.settings;

namespace llm_protector.protection;

public class ProtectionMiddleware(RequestDelegate next)
{
    private LogDecorator _logger;
    public async Task InvokeAsync(
        HttpContext context,
        DecodingService decoding,
        SettingsService settingsService,
        PatternFilter patternFilter,
        TfIdfFilter tfIdfFilter,
        OnnxFilter onnxFilter,
        LogDecorator logger,
        VectorFilter vectorFilter)
    {
        if (!settingsService.IsFilterActive) return;
        _logger = logger;
        
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        
        var messages = ExtractMessages(body);
        foreach (var message in messages)
        {
            var processedMessage = message with { Content = decoding.DecodeMessage(message.Content) };
            
            if (!patternFilter.MessageContainsDangerPattern(processedMessage.Content)) continue;
            
            var sw = Stopwatch.StartNew();
            
            float tfidfScore = tfIdfFilter.GetInjectionProbability(processedMessage.Content);
            switch (tfidfScore)
            {
                case > 0.9f:
                    await BlockRequest(context, BlockReason.TF_IDF);
                    sw.Stop();
                    logger.MachineLearningProcess(sw.ElapsedMilliseconds);
                    return;
                case < 0.1f:
                    continue;
            }

            float vectorScore = await vectorFilter.CalculateSimilarity(message.Content);
            switch (vectorScore)
            {
                case > 0.9f:
                    await BlockRequest(context, BlockReason.EMBEDDING);
                    return;
                case < 0.5f:
                    continue;
            }

            float onnxScore = onnxFilter.GetInjectionProbability(processedMessage.Content);
            if (onnxScore > 0.9f)
            {
                await BlockRequest(context, BlockReason.TRANSFORMER);
                sw.Stop();
                logger.MachineLearningProcess(sw.ElapsedMilliseconds);
                return;
            }
            sw.Stop();
            logger.MachineLearningProcess(sw.ElapsedMilliseconds);
        }
    }

    private List<PromptMessage> ExtractMessages(string body)
    {
        var messages = new List<PromptMessage>();
        try
        {
            var json = JObject.Parse(body);
            var messageArray = json["messages"] as JArray;

            if (messageArray != null)
            {
                foreach (var item in messageArray)
                {
                    MessageRole role = Enum.Parse<MessageRole>(item["role"]?.ToString().ToUpper() ?? "USER");
                    string content = item["content"]?.ToString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(content))
                    {
                        messages.Add(new PromptMessage(role, content));
                    }
                }
            }
        }
        catch (Exception e)
        {
            //todo: add log
        }
        return messages;
    }

    private async Task BlockRequest(HttpContext context, BlockReason reason)
    {
        _logger.LogBlocked(reason.ToString());
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Security Policy Violation",
            message = "Your request has been blocked by LLMSP security filters",
        });
    }
}

public enum BlockReason
{
    TF_IDF,
    EMBEDDING,
    TRANSFORMER
}

public enum MessageRole
{
    USER,
    SYSTEM
}
public record PromptMessage(MessageRole Role, string Content);