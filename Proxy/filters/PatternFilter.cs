using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using llm_protector.decorators;
using Shared;

namespace llm_protector.static_filter;

public class PatternFilter(LogDecorator logger, DatabaseService db)
{
    public bool MessageContainsDangerPattern(string message)
    {
        var sw = Stopwatch.StartNew();
        using var conn = db.CreateConnection();
        var activePatterns = conn.Query<(string Pattern, string FileName)>(@"
            SELECT i.ItemValue, f.FileName 
            FROM RiskFileItems i
            JOIN RiskFiles f ON i.FileId = f.Id
            WHERE f.IsActive = 1 AND f.FileType = 'PATTERN';").ToList();
        
        foreach (var (item, index) in activePatterns.Select((item, index) => (item, index)) )
        {
            try
            {
                if (Regex.IsMatch(message, item.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    sw.Stop();
                    logger.LogPatternFound(sw.ElapsedMilliseconds, item.Pattern, item.FileName, index);
                    logger.LogStaticFilterProcess(sw.ElapsedMilliseconds, true);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Ongeldige Regex in {item.FileName}: {item.Pattern}. Fout: {e.Message}");
            }
        }

        sw.Stop();
        logger.LogStaticFilterProcess(sw.ElapsedMilliseconds, false);
        return false;
    }
}