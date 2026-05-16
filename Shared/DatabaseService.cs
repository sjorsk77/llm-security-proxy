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
        
        cmd.CommandText = "SELECT COUNT(*) FROM Filters;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Filters (Type, Name, Description, IsEnabled) VALUES 
                ('WORD', 'Sensitive word filter', 'Scans incoming prompts on sensitive words.', 1),
                ('PATTERN', 'Regex Pattern filter', 'Uses regular expressions to filter out sensitive patterns.', 1);";
            cmd.ExecuteNonQuery();
            Console.WriteLine("[SYS_DB] Database succesvol geïnitialiseerd en gevuld met standaard filters.");
        }
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "SELECT COUNT(*) FROM Settings;";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Settings (Key, Value) VALUES 
                ('IsFilterActive', '1'),
                ('CustomBlockMessage', 'Safety Guard: Request blocked');";
            cmd.ExecuteNonQuery();
        }
    }
}