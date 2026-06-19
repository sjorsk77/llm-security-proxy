using System.Diagnostics;
using llm_protector.decorators;
using llm_protector.filters;
using llm_protector.ml;
using llm_protector.static_filter;
using Newtonsoft.Json.Linq;
using Shared.settings;

namespace llm_protector.protection;

public class ProtectionMiddleware
{
    private readonly SettingsService _settingsService;
    private LogDecorator _logger;

    public ProtectionMiddleware(RequestDelegate next, SettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public async Task InvokeAsync(
        HttpContext context,
        DecodingService decoding,
        PatternFilter patternFilter,
        TfIdfFilter tfIdfFilter,
        OnnxFilter onnxFilter,
        LogDecorator logger,
        VectorFilter vectorFilter)
    {
        if (context.Request.Path.Equals("/api/sync-vectors", StringComparison.CurrentCultureIgnoreCase))
        {
            _ = Task.Run(async () => await vectorFilter.SyncQuadrantFiles());
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            await context.Response.WriteAsync("Sync Vectors");
            return;
        }
        
        
        if (!_settingsService.IsFilterActive) return;
        _logger = logger;
        
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        
        var messages = ExtractMessages(body);
        foreach (var message in messages)
        {
            var processedMessage = message with { Content = decoding.DecodeMessage(message.Content) };

            switch (_settingsService.PatternType)
            {
                case PatternType.Contains:
                    if (!patternFilter.MessageContainsDangerPattern(processedMessage.Content)) continue;
                    break;
                case PatternType.HardBlock:
                    if (patternFilter.MessageContainsDangerPattern(processedMessage.Content))
                    {
                        await BlockRequest(context, BlockReason.PATTERN);
                        return;
                    }
                    break;
            }

            var sw = Stopwatch.StartNew();
            if (_settingsService.TfIdfActive)
            {
                float tfidfScore = tfIdfFilter.GetInjectionProbability(processedMessage.Content);

                if (tfidfScore >= _settingsService.TfIdfFail / 100)
                {
                    await BlockRequest(context, BlockReason.TF_IDF);
                    sw.Stop();
                    logger.MachineLearningProcess(sw.ElapsedMilliseconds);
                    return;
                }  
                if (tfidfScore <= _settingsService.TfIdfPass / 100) continue;
            }

            if (_settingsService.EmbeddingActive)
            {
                float vectorScore = await vectorFilter.CalculateSimilarity(message.Content);
                
                if (vectorScore >= _settingsService.EmbeddingFail / 100)
                {
                    await BlockRequest(context, BlockReason.EMBEDDING);
                    sw.Stop();
                    logger.MachineLearningProcess(sw.ElapsedMilliseconds);
                    return;
                }
                if (vectorScore <= _settingsService.EmbeddingPass / 100) continue;
            }

            if (_settingsService.TransformerActive)
            {
                float onnxScore = onnxFilter.GetInjectionProbability(processedMessage.Content);
                if (onnxScore >= _settingsService.TransformerFail / 100)
                {
                    await BlockRequest(context, BlockReason.TRANSFORMER);
                    sw.Stop();
                    logger.MachineLearningProcess(sw.ElapsedMilliseconds);
                    return;
                }
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
            message = _settingsService.CustomBlockMessage,
        });
    }
}

public enum BlockReason
{
    PATTERN,
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