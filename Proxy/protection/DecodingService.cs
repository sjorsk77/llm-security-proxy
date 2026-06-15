using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using llm_protector.decorators;
using Shared.settings;

namespace llm_protector.protection;

public enum DecodingMethod
{
    HTML,
    BASE64,
    URL,
    UNICODE
}

public record DecodingResult(string DecodedContent, long ElapsedMs, DecodingMethod Method);

public class DecodingService(
    LogDecorator logger,
    SettingsService settings
    )
{
    private DecodingResult DecodeStep(string input, DecodingMethod method, Func<string, string> decodeAction)
    {
        var sw = Stopwatch.StartNew();
        var result = decodeAction(input);
        sw.Stop();
        
        logger.LogDecodingStep(method, sw.ElapsedMilliseconds, result != input);
            
        return new DecodingResult(result, sw.ElapsedMilliseconds, method);
    }

    public string DecodeMessage(string prompt)
    {
        var sw = Stopwatch.StartNew();
        var currentPrompt = prompt;
        string previous;
        int iterations = 0;
        int maxIterations = settings.RecursiveDecoding ? settings.RecursiveDepth : 1;

        do
        {
            previous = currentPrompt;
        
            if (settings.DecodeHtml)
                currentPrompt = DecodeStep(currentPrompt, DecodingMethod.HTML, WebUtility.HtmlDecode).DecodedContent;
            
            if (settings.DecodeUrl)
                currentPrompt = DecodeStep(currentPrompt, DecodingMethod.URL, WebUtility.UrlDecode).DecodedContent;
            
            if (settings.DecodeBase64)
            {
                currentPrompt = DecodeStep(currentPrompt, DecodingMethod.BASE64, input => 
                {
                    string base64Pattern = @"[a-zA-Z0-9+/]{12,}=*";
                    return Regex.Replace(input, base64Pattern, match => {
                        string val = match.Value;
                        if (val.Length % 4 != 0) return val;
    
                        try {
                            byte[] data = Convert.FromBase64String(val);
                            string decoded = Encoding.UTF8.GetString(data);
                            
                            return IsPrintable(decoded) ? decoded : val;
                        } catch { return val; }
                    });
                }).DecodedContent;
            }

            if (settings.DecodeUnicode)
            {
                currentPrompt = DecodeStep(currentPrompt, DecodingMethod.UNICODE, input => 
                {
                    string unicodeEscapePattern = @"\\u[0-9A-Fa-f]{4}";
                    var decoded = Regex.Replace(input, unicodeEscapePattern, match => {
                        try { return char.ConvertFromUtf32(int.Parse(match.Value.Substring(2), System.Globalization.NumberStyles.HexNumber)); }
                        catch { return match.Value; }
                    });

                    decoded = decoded.Normalize(NormalizationForm.FormD);
                    StringBuilder sb = new StringBuilder();
                    foreach (var c in decoded)
                    {
                        if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                            sb.Append(c);
                    }
                    return sb.ToString().Normalize(NormalizationForm.FormC);
                }).DecodedContent;
            }
        
            iterations++;
        } while (currentPrompt != previous && iterations < maxIterations);
        
        sw.Stop();
        
        logger.LogDecodingProcess(prompt != currentPrompt, sw.ElapsedMilliseconds, iterations);
    
        return currentPrompt;
    }
    private bool IsPrintable(string input)
    {
        return input.All(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c));
    }
    
}