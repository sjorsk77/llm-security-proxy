namespace llm_protector.protection.riskfiles;

public class RiskFileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public FileType FileType { get; set; } = FileType.WORD;
    public bool IsActive { get; set; } = true;
}

public enum FileType
{
    WORD,
    PATTERN
}