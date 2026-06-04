using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using llm_protector.log;
using llm_protector.ml;
using llm_protector.protection.filter;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.settings;

namespace llm_protector.protection;

public class PromptProtectionService(
    FilterManagementService filterService,
    DecodingService decoding,
    DatabaseService db, 
    LogDecorator logger)
{
    public bool PassRequest(string body, out string reason)
    {
        var json = JObject.Parse(body);
        var content = json["messages"]?[0]?["content"]?.ToString();
        
        reason = string.Empty;
        if (content == null) return false;
        
        var processedBody = decoding.DecodeMessage(content);
        
        var pipeline = BuildPipeline();

        foreach (var f in pipeline)
        {
            var result = f(processedBody);
            if (!result.IsMatch) continue;
            reason = result.Reason;
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
        var sw = Stopwatch.StartNew();
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
                sw.Stop();
                logger.LogStaticFilterStep(StaticFilterType.WORD, sw.ElapsedMilliseconds, true);
                return (true, $"Geflagged door woord-bestand [{item.FileName}] vanwege: '{item.Word}'");
            }
        }
        

        sw.Stop();
        logger.LogStaticFilterStep(StaticFilterType.WORD, sw.ElapsedMilliseconds, false);
        return (false, string.Empty);
    }
    
    private (bool IsMatch, string Reason) RunPatternScan(string normalizedBody)
    {
        var sw = Stopwatch.StartNew();
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
                    sw.Stop();
                    logger.LogStaticFilterStep(StaticFilterType.PATTERN, sw.ElapsedMilliseconds, true);
                    return (true, $"Geflagged door regex-bestand [{item.FileName}] vanwege patroon match");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Ongeldige Regex in {item.FileName}: {item.Pattern}. Fout: {e.Message}");
            }
        }

        sw.Stop();
        logger.LogStaticFilterStep(StaticFilterType.PATTERN, sw.ElapsedMilliseconds, false);
        return (false, string.Empty);
    }
}