using Dapper;
using Parquet;
using Shared;

namespace llm_protector.protection.riskfiles;

public class TrainingDataService
{
    private readonly DatabaseService _db;

    public TrainingDataService(DatabaseService db)
    {
        _db = db;
    }
    
    public void ImportPromptsFromFile(string content, string fileName, bool isInjection)
    {
        var lines = content.Split(new[] { ",", "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
        
        using var conn = _db.CreateConnection();
        
        using var transaction = conn.BeginTransaction();
    
        var sql = @"INSERT INTO PromptTrainingData (PromptText, IsInjection, FileName) 
                VALUES (@PromptText, @IsInjection, @FileName)";
    
        foreach (var line in lines)
        {
            conn.Execute(sql, new { 
                PromptText = line, 
                IsInjection = isInjection ? 1 : 0, 
                FileName = fileName 
            }, transaction);
        }
    
        transaction.Commit();
    }
    
    public void DeleteDataByFile(string fileName)
    {
        using var conn = _db.CreateConnection();
        conn.Execute("DELETE FROM PromptTrainingData WHERE FileName = @fileName", new { fileName });
    }
    
    public List<string> GetUploadedFiles()
    {
        using var conn = _db.CreateConnection();
        return conn.Query<string>("SELECT DISTINCT FileName FROM PromptTrainingData").ToList();
    }

    public int GetCountForFile(string fileName)
    {
        using var conn = _db.CreateConnection();
        return conn.QuerySingle<int>("SELECT COUNT(*) FROM PromptTrainingData WHERE FileName = @fileName", new { fileName });
    }

    public async Task UploadParquetToSqlDatabase(Stream fileStream, string fileName)
    {
        await using var reader = await ParquetReader.CreateAsync(fileStream);
        
        var schema = reader.Schema;
        var fields = schema.DataFields.ToList();
        
        int textIdx = fields.FindIndex(f => f.Name.Equals("text", StringComparison.OrdinalIgnoreCase));
        int labelIdx = fields.FindIndex(f => f.Name.Equals("label", StringComparison.OrdinalIgnoreCase));

        if (textIdx == -1 || labelIdx == -1) throw new Exception("Column not found.");

        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using var groupReader = reader.OpenRowGroupReader(i);
            int rowCount = (int)groupReader.RowCount;
            
            string?[] promptBuffer = new string?[rowCount];
            int?[] labelBuffer = new int?[rowCount];
            
            int[] promptDefLevels = new int[rowCount];
            int[] labelDefLevels = new int[rowCount];
            
            Memory<string?> promptMemory = promptBuffer;
            Memory<int?> labelMemory = labelBuffer;
            
            Memory<int> promptDefMemory = promptDefLevels;
            Memory<int> labelDefMemory = labelDefLevels;
            
            await groupReader.ReadAsync(fields[textIdx], promptMemory, promptDefMemory, CancellationToken.None);
            await groupReader.ReadAsync(fields[labelIdx], labelMemory, labelDefMemory, CancellationToken.None);

            var dataToInsert = promptBuffer.Select((prompt, index) => new
            {
                Prompt = prompt,
                IsMalicious = (labelBuffer[index] == 1),
                FileName = fileName
            });
            
            using var connection = _db.CreateConnection();
            await connection.OpenAsync();
        
            var sql = "INSERT INTO PromptTrainingData (PromptText, IsInjection, IsSyncedToVectorDb, FileName) VALUES (@Prompt, @IsMalicious, 0, @FileName)";
        
            await connection.ExecuteAsync(sql, dataToInsert);
        }
    }
}