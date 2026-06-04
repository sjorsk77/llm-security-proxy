using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using llm_protector.ml;
using llm_protector.protection.filter;
using Shared;
using Shared.settings;

namespace llm_protector.protection;

public class PromptProtectionService(
    FilterManagementService filterService,
    DecodingService decoding,
    DatabaseService db, 
    SettingsService settings,
    PromptMlClassifier mlClassifier,
    ILogger<PromptProtectionService> logger)
{
    public bool PassRequest(string body, out string reason)
    {
        reason = string.Empty;
        //first decode the body to find encoded hidden prompts.
        var processedBody = decoding.PreprocessBody(body);
        
        //run the safety pipeline
        var pipeline = BuildPipeline();

        foreach (var f in pipeline)
        {
            var result = f(processedBody);
            if (!result.IsMatch) continue;
            reason = result.Reason;
            return true;
        }

        var (isMalicious, probability) = mlClassifier.AnalyzePrompt(processedBody);
        
        if (isMalicious && probability >= 0.75f)
        {
            reason = $"Flagged by local ML.NET Classifier (Confidence: {probability:P1}) due to hostile semantic pattern.";
            return true;
        }
        
        return false;
    }
    
    private List<Func<string, (bool IsMatch, string Reason)>> BuildPipeline()
    {
        var pipeline = new List<Func<string, (bool IsMatch, string Reason)>>();
        
        var wordFilter = filterService.GetFilters().FirstOrDefault(f => f.Type == FilterType.WORD);
        if (wordFilter is { IsEnabled: true })
        {
            pipeline.Add(input => RunWordScan(input));
        }
        
        var patternFilter = filterService.GetFilters().FirstOrDefault(f => f.Type == FilterType.PATTERN);
        if (patternFilter is { IsEnabled: true })
        {
            pipeline.Add(input => RunPatternScan(input));
        }
        
        return pipeline;
    }
    
    private (bool IsMatch, string Reason) RunWordScan(string normalizedBody)
    {
        using var conn = db.CreateConnection();
        var activeWords = conn.Query<(string Word, string FileName)>(@"
            SELECT i.ItemValue, f.FileName 
            FROM RiskFileItems i
            JOIN RiskFiles f ON i.FileId = f.Id
            WHERE f.IsActive = 1 AND f.FileType = 'WORD';").ToList();

        foreach (var item in activeWords)
        {
            if (normalizedBody.Contains(item.Word, StringComparison.OrdinalIgnoreCase))
            {
                return (true, $"Geflagged door woord-bestand [{item.FileName}] vanwege: '{item.Word}'");
            }
        }

        return (false, string.Empty);
    }
    
    private (bool IsMatch, string Reason) RunPatternScan(string normalizedBody)
    {
        using var conn = db.CreateConnection();
        var activePatterns = conn.Query<(string Pattern, string FileName)>(@"
            SELECT i.ItemValue, f.FileName 
            FROM RiskFileItems i
            JOIN RiskFiles f ON i.FileId = f.Id
            WHERE f.IsActive = 1 AND f.FileType = 'PATTERN';").ToList();

        foreach (var item in activePatterns)
        {
            try
            {
                if (Regex.IsMatch(normalizedBody, item.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    return (true, $"Geflagged door regex-bestand [{item.FileName}] vanwege patroon match");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Ongeldige Regex in {item.FileName}: {item.Pattern}. Fout: {e.Message}");
            }
        }

        return (false, string.Empty);
    }
    
    private string PreprocessBody(string prompt)
{
    string previousPrompt;
    int iterations = 0;
    
    int maxIterations = settings.RecursiveDecoding ? settings.RecursiveDepth : 1;

    do
    {
        previousPrompt = prompt;
        iterations++;
        
        if (settings.DecodeHtml)
            prompt = WebUtility.HtmlDecode(prompt);
        
        if (settings.DecodeUrl)
        {
            string hexPattern = @"(?:%[0-9A-Fa-f]{2})+";
            prompt = Regex.Replace(prompt, hexPattern, match => WebUtility.UrlDecode(match.Value));
        }
        
        if (settings.DecodeBase64)
        {
            string base64Pattern = @"[a-zA-Z0-9+/]{12,}=*";
            prompt = Regex.Replace(prompt, base64Pattern, match => {
                try {
                    if (match.Value.Length % 4 != 0) return match.Value;
                    byte[] data = Convert.FromBase64String(match.Value);
                    string decoded = Encoding.UTF8.GetString(data);
                    return decoded.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)) ? match.Value : decoded;
                } catch { return match.Value; }
            });
        }
        
        if (settings.DecodeUnicode)
        {
            string unicodeEscapePattern = @"\\u[0-9A-Fa-f]{4}";
            prompt = Regex.Replace(prompt, unicodeEscapePattern, match => {
                try {
                    int code = int.Parse(match.Value.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    return char.ConvertFromUtf32(code);
                } catch { return match.Value; }
            });

            prompt = prompt.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (var c in prompt)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            prompt = sb.ToString().Normalize(NormalizationForm.FormC);
        }
        
    } while (prompt != previousPrompt && iterations < maxIterations);
    
    return prompt;
}
}