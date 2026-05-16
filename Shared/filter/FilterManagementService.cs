using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Shared;

namespace llm_protector.protection.filter;

public class FilterManagementService
{
    private readonly DatabaseService _db;
    
    public FilterManagementService(DatabaseService db)
    {
        _db = db;
    }

    public List<FilterDefinition> GetFilters()
    {
        using var conn = _db.CreateConnection();
        return conn.Query<FilterDefinition>("SELECT * FROM Filters").ToList();
    }
    
    public bool IsFilterEnabled(FilterType type)
    {
        using var conn = _db.CreateConnection();
        
        return conn.QueryFirstOrDefault<bool>(
            "SELECT IsEnabled FROM Filters WHERE Type = @type;", 
            new { type = type.ToString() }
        );
    }
    
    public void ToggleFilter(FilterType filterType, bool isEnabled)
    {
        using var conn = _db.CreateConnection();
        
        conn.Execute(
            "UPDATE Filters SET IsEnabled = @enabled WHERE Type = @type;", 
            new { 
                enabled = isEnabled ? 1 : 0, 
                type = filterType.ToString() 
            }
        );
    }
}