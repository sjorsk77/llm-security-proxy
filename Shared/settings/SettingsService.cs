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
        
        if (typeof(T).IsEnum)
        {
            return (T)Enum.Parse(typeof(T), rawValue);
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
    
    public bool TfIdfActive
    {
        get => GetValue<bool>("TfIdfActive");
        set => SetValue("TfIdfActive", value);
    }
    
    public float TfIdfPass
    {
        get => GetValue<float>("TfIdfPass");
        set => SetValue("TfIdfPass", value > 100 ? 100 : value);
    }
    
    public float TfIdfFail
    {
        get => GetValue<float>("TfIdfFail");
        set => SetValue("TfIdfFail", value > 100 ? 100 : value);
    }
    public bool EmbeddingActive
    {
        get => GetValue<bool>("EmbeddingActive");
        set => SetValue("EmbeddingActive", value);
    }
    
    public float EmbeddingPass
    {
        get => GetValue<float>("EmbeddingPass");
        set => SetValue("EmbeddingPass", value > 100 ? 100 : value);
    }
    
    public float EmbeddingFail
    {
        get => GetValue<float>("EmbeddingFail");
        set => SetValue("EmbeddingFail", value > 100 ? 100 : value);
    }
    
    public bool TransformerActive
    {
        get => GetValue<bool>("TransformerActive");
        set => SetValue("TransformerActive", value);
    }
    
    public float TransformerFail
    {
        get => GetValue<float>("TransformerFail");
        set => SetValue("TransformerFail", value > 100 ? 100 : value);
    }
    
    public PatternType PatternType
    {
        get => GetValue<PatternType>("PatternType");
        set => SetValue("PatternType", value);
    }
}

public enum PatternType
{
    Off,
    HardBlock,
    Contains
}