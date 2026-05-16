using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using llm_protector.protection.filter;
using Shared;

namespace llm_protector.protection;

public class PromptProtectionService(FilterManagementService filterService, DatabaseService db)
{
    public bool IsNotSafe(string body, out string reason)
    {
        reason = string.Empty;
        var normalizedBody = NormalizeEncoding(body);
        var pipeline = BuildPipeline(normalizedBody);

        foreach (var f in pipeline)
        {
            var result = f(normalizedBody);
            if (!result.IsMatch) continue;
            reason = result.Reason;
            return true;
        }
        
        return false;
    }
    
    private List<Func<string, (bool IsMatch, string Reason)>> BuildPipeline(string normalizedBody)
    {
        var pipeline = new List<Func<string, (bool IsMatch, string Reason)>>();
        
        var wordFilter = filterService.GetFilters().FirstOrDefault(f => f.Type == FilterType.WORD);
        if (wordFilter is { IsEnabled: true }) pipeline.Add(input => RunWordScan(normalizedBody));
        
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
    
    private static string NormalizeEncoding(string prompt)
    {
        prompt = WebUtility.HtmlDecode(prompt);
        
        string hexPattern = @"(?:%[0-9A-Fa-f]{2}){3,}";
        prompt = Regex.Replace(prompt, hexPattern, match => WebUtility.UrlDecode(match.Value));
        
        string base64Pattern = @"[a-zA-Z0-9+/]{12,}=*";
        prompt = Regex.Replace(prompt, base64Pattern, match => {
            try {
                if (match.Value.Length % 4 != 0) return match.Value;
                byte[] data = Convert.FromBase64String(match.Value);
                string decoded = Encoding.UTF8.GetString(data);
                return decoded.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)) ? match.Value : decoded;
            } catch { return match.Value; }
        });
        
        prompt = prompt.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();
        foreach (var c in prompt)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        prompt = sb.ToString().Normalize(NormalizationForm.FormC);
        
        return prompt;
    }
}