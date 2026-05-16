namespace llm_protector.protection.filter;

public class FilterDefinition
{
    public FilterType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public enum FilterType
{
    WORD,
    PATTERN
}