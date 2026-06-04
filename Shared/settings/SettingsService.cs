using System.Text.Json;
using Dapper;

namespace Shared.settings;

public class SettingsService
{
    private readonly DatabaseService _db;

    public SettingsService(DatabaseService db)
    {
        _db = db;
    }
    
    private T GetValue<T>(string key)
    {
        using var conn = _db.CreateConnection();
        var rawValue = conn.QueryFirstOrDefault<string>("SELECT Value FROM Settings WHERE Key = @Key;", new { Key = key });
        
        if (string.IsNullOrEmpty(rawValue))
        {
            return default!;
        }
        
        if (typeof(T) == typeof(string))
        {
            return (T)(object)rawValue;
        }

        if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
        {
            return (T)Convert.ChangeType(rawValue, typeof(T));
        }

        try
        {
            return JsonSerializer.Deserialize<T>(rawValue)!;
        }
        catch (JsonException)
        {
            return (T)Convert.ChangeType(rawValue, typeof(T));
        }
    }
    private void SetValue<T>(string key, T value)
    {
        using var conn = _db.CreateConnection();
        string rawValue = typeof(T) == typeof(string) 
            ? value?.ToString() ?? "" 
            : JsonSerializer.Serialize(value);

        conn.Execute(@"
            INSERT OR REPLACE INTO Settings (Key, Value) 
            VALUES (@Key, @Value);", 
            new { Key = key, Value = rawValue });
    }
    
    public bool IsFilterActive
    {
        get => GetValue<bool>("IsFilterActive");
        set => SetValue("IsFilterActive", value);
    }

    public string CustomBlockMessage
    {
        get => GetValue<string>("CustomBlockMessage");
        set => SetValue("CustomBlockMessage", value);
    }

    public string BackendUrl
    {
        get => GetValue<string>("BackendUrl");
        set => SetValue("BackendUrl", value);
    }
    
    public bool DecodeUrl
    {
        get => GetValue<bool>("DecodeUrl");
        set => SetValue("DecodeUrl", value);
    }

    public bool DecodeBase64
    {
        get => GetValue<bool>("DecodeBase64");
        set => SetValue("DecodeBase64", value);
    }

    public bool DecodeHtml
    {
        get => GetValue<bool>("DecodeHtml");
        set => SetValue("DecodeHtml", value);
    }

    public bool DecodeUnicode
    {
        get => GetValue<bool>("DecodeUnicode");
        set => SetValue("DecodeUnicode", value);
    }
    
    public bool RecursiveDecoding
    {
        get => GetValue<bool>("RecursiveDecoding");
        set => SetValue("RecursiveDecoding", value);
    }
    
    public int RecursiveDepth
    {
        get => GetValue<int>("RecursiveDepth");
        set => SetValue("RecursiveDepth", value);
    }
}