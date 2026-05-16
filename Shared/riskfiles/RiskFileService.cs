using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Shared;

namespace llm_protector.protection.riskfiles;

public class RiskFileService
{
    private readonly DatabaseService _db;

    public RiskFileService(DatabaseService db)
    {
        _db = db;
    }
    
    public List<RiskFileMetadataDto> GetRiskFiles()
    {
        using var conn = _db.CreateConnection();
        return conn.Query<RiskFileMetadataDto>("SELECT Id, FileName, FileType, IsActive FROM RiskFiles;").ToList();
    }
    
    public async Task SaveUploadedFileAsync(string fileName, string content, FileType fileType)
    {
        var safeFileName = Path.GetFileName(fileName);
        
        var items = content.Split(new[] { ",", "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        using var conn = _db.CreateConnection();
        
        using var transaction = conn.BeginTransaction();

        try
        {
            conn.Execute("DELETE FROM RiskFiles WHERE FileName = @FileName;", new { FileName = safeFileName }, transaction);
            
            var fileId = conn.QuerySingle<int>(@"
                INSERT INTO RiskFiles (FileName, FileType, IsActive) 
                VALUES (@FileName, @FileType, 1);
                SELECT last_insert_rowid();", 
                new { FileName = safeFileName, FileType = fileType.ToString() }, 
                transaction
            );
            
            var dapperItems = items.Select(item => new { FileId = fileId, ItemValue = item });
            conn.Execute("INSERT INTO RiskFileItems (FileId, ItemValue) VALUES (@FileId, @ItemValue);", dapperItems, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public void ToggleFileStatus(string fileName, bool isActive)
    {
        using var conn = _db.CreateConnection();
        conn.Execute("UPDATE RiskFiles SET IsActive = @IsActive WHERE FileName = @FileName;", 
            new { IsActive = isActive ? 1 : 0, FileName = fileName });
    }

    public void UpdateFileType(string fileName, FileType newFileType)
    {
        using var conn = _db.CreateConnection();
        conn.Execute("UPDATE RiskFiles SET FileType = @FileType WHERE FileName = @FileName;", 
            new { FileType = newFileType.ToString(), FileName = fileName });
    }

    public void DeleteFile(string fileName)
    {
        using var conn = _db.CreateConnection();
        conn.Execute("DELETE FROM RiskFiles WHERE FileName = @FileName;", new { FileName = fileName });
    }
}

public class RiskFileMetadataDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}