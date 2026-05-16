using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using dotenv.net;

namespace llm_protector.protection;

public class PromptProtectionService
{
    public bool IsNotSafe(string body)
    {
        return ContainsRiskWords(body);
    }

    private static bool ContainsRiskWords(string body)
    {
        var riskFilePath = Environment.GetEnvironmentVariable("RISK_WORD_FILEPATH");

        if (riskFilePath == null) return false;

        if (!File.Exists(riskFilePath)) return false;
        
        var content = File.ReadAllText(riskFilePath);

        var riskKeywords = content.Split(',')
            .Select(word => word.Trim())
            .Where(word => !string.IsNullOrEmpty(word))
            .ToList();

        return riskKeywords.Any(word => body.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
    
    private static bool ContainsRiskPatterns(string prompt)
    {
        var riskFilePath = Environment.GetEnvironmentVariable("RISK_WORD_FILEPATH");

        if (riskFilePath == null) return false;

        if (!File.Exists(riskFilePath)) return false;
        
        var content = File.ReadAllText(riskFilePath);

        var riskPatterns = content.Split(',')
            .ToList();

        return !riskPatterns.Any(pattern =>
        {
            try
            {
                return Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Invalid Regex : {pattern}. Error: {e}");
                return false;
            }
        });
    }
    
    //Add structured prompts with clear seperation to check for an attack
    private static bool LlmValidation(string prompt)
    {
        return false;
    }

    //Sanitize encoding attempts
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