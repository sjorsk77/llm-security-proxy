using Microsoft.Data.Sqlite;

namespace Shared;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var currentFolder = new DirectoryInfo(AppContext.BaseDirectory);
        
        while (currentFolder != null && 
               !File.Exists(Path.Combine(currentFolder.FullName, "llm-protector.sln")) &&
               currentFolder.Name != "llm-protector")
        {
            currentFolder = currentFolder.Parent;
        }
        
        var rootPath = currentFolder != null ? currentFolder.FullName : AppContext.BaseDirectory;
        var dataFolder = Path.Combine(rootPath, "data");
        
        if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
        
        var dbPath = Path.Combine(dataFolder, "storage.db");
        
        _connectionString = $"Data Source={dbPath};Cache=Shared;";

        InitializeDatabase();
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();

        return conn;
    }

    private void InitializeDatabase()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Filters (
                Type TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                RequestBody TEXT NOT NULL,
                IsBlocked INTEGER NOT NULL,
                Reason TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS RiskFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL UNIQUE,
                FileType TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS RiskFileItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileId INTEGER NOT NULL,
                ItemValue TEXT NOT NULL,
                FOREIGN KEY(FileId) REFERENCES RiskFiles(Id) ON DELETE CASCADE
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PromptTrainingData (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PromptText TEXT NOT NULL,
                IsInjection INTEGER NOT NULL,
                IsSyncedToVectorDb INTEGER NOT NULL,
                FileName TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "SELECT COUNT(*) FROM Filters;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Filters (Type, Name, Description, IsEnabled) VALUES 
                ('WORD', 'Sensitive word filter', 'Scans incoming prompts on sensitive words.', 1),
                ('PATTERN', 'Regex Pattern filter', 'Uses regular expressions to filter out sensitive patterns.', 1);";
            cmd.ExecuteNonQuery();
        }
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = @"
            INSERT OR IGNORE INTO Settings (Key, Value) VALUES 
            ('IsFilterActive', 'true'),
            ('CustomBlockMessage', 'Safety Guard: Request blocked'),
            ('BackendUrl', 'https://api.openai.com/v1'),
            ('DecodeUrl', 'true'),
            ('DecodeBase64', 'true'),
            ('DecodeHtml', 'true'),
            ('DecodeUnicode', 'true'),
            ('RecursiveDecoding', 'true'),
            ('RecursiveDepth', '3'),
            ('PatternType', 'HardBlock'),
            ('EmbeddingActive', 'true'),
            ('EmbeddingPass', '30'),
            ('EmbeddingFail', '90'),
            ('TransformerActive', 'true'),
            ('TransformerFail', '90'),
            ('TfIdfActive', 'true'),
            ('TfIdfPass', '50'),
            ('TfIdfFail', '90');";
        
        cmd.ExecuteNonQuery();
    }
}