using System.Text.Json;
using Dapper;
using Shared;

namespace llm_protector;

public class ProxyLogService
{
    private readonly DatabaseService _db;
    
    public ProxyLogService(DatabaseService db)
    {
        _db = db;
    }

    public void AddLogEntry(string body, bool isBlocked, string reason = "")
    {
        using var conn = _db.CreateConnection();

        // 1. Voeg de log toe via een snelle Dapper insert statement
        conn.Execute(@"
            INSERT INTO Logs (Timestamp, RequestBody, IsBlocked, Reason) 
            VALUES (@Timestamp, @RequestBody, @IsBlocked, @Reason);", 
            new { 
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), 
                RequestBody = body, 
                IsBlocked = isBlocked ? 1 : 0, 
                Reason = reason 
            }
        );
        
        // 2. Optioneel: Ruim de database automatisch op en bewaar alleen de laatste 200 logs
        conn.Execute("DELETE FROM Logs WHERE Id NOT IN (SELECT Id FROM Logs ORDER BY Id DESC LIMIT 200);");
    }
    public List<LogEntry> GetRecentLogs()
    {
        using var conn = _db.CreateConnection();
        return conn.Query<LogEntry>("SELECT Timestamp, RequestBody, IsBlocked, Reason FROM Logs ORDER BY Id DESC LIMIT 200;").ToList();
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string RequestBody { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public string Reason { get; set; } = string.Empty;
}