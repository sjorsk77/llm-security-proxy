using Dapper;
using llm_protector.ml_helpers;
using llm_protector.ml;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Shared;

namespace llm_protector.filters;

public class VectorFilter
{
    private readonly DatabaseService _db;
    private readonly QdrantClient _qdrantClient;
    private readonly EmbeddingHelper _embedder;
    private readonly int _cunkSize = 30;
    private readonly int _overlap = 10;
    
    public VectorFilter(DatabaseService db, EmbeddingHelper embedder)
    {
        _embedder = embedder;
        _db = db;
        _qdrantClient = new QdrantClient("localhost", port: 6334);
    }

    public async Task<float> CalculateSimilarity(string message, float similarityThreshold = 0.9f)
    {
        var segments = GetMessageSegments(message);
        var maxSimilarity = 0f;

        foreach (var segment in segments)
        {
            float[] segmentVector = await _embedder.GenerateEmbeddingAsync(segment);

            var searchResult = await _qdrantClient.SearchAsync(
    "jailbreak_prompts",
                segmentVector,
                limit:1
            );

            if (searchResult.Count <= 0) continue;
            float similarity = searchResult.First().Score;
            if (similarity > similarityThreshold) return similarity;
            if (similarity > maxSimilarity) maxSimilarity = similarity;
        }
        
        return maxSimilarity;
    }

    public async Task SyncQuadrantFiles()
    {
        using var connection = _db.CreateConnection();
        var unsyncedItems = await connection.QueryAsync(
            "SELECT * FROM PromptTrainingData WHERE IsSyncedToVectorDb = 0");

        if (!unsyncedItems.Any()) return;

        var points = new List<PointStruct>();

        foreach (var unsyncedItem in unsyncedItems)
        {
            float[] vector = await _embedder.GenerateEmbeddingAsync(unsyncedItem.PromptText);
            
            points.Add(new PointStruct
            {
                Id = new PointId(),
                Vectors = vector,
                Payload = {{ "prompt", unsyncedItem.PromptText }, {"is_injection", unsyncedItem.IsInjection}}
            });

            await _qdrantClient.UpsertAsync("jailbreak_prompts", points);

            var ids = unsyncedItems.Select(i => i.Id).ToList();
            await connection.ExecuteAsync(
                "UPDATE PromptTrainingData SET IsSyncedToVectorDb = 1 WHERE Id IN @Ids", 
                new { Ids = ids });
        }
    }

    private List<string> GetMessageSegments(string content)
    {
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>();
        
        if (words.Length <= _cunkSize) return new List<string> { content };
        
        for (int i = 0; i < words.Length; i += (_cunkSize - _overlap))
        {
            var chunk = words.Skip(i).Take(_cunkSize);
            segments.Add(string.Join(" ", chunk));
            if (i + 30 >= words.Length) break;
        }
        
        return segments;
    }
}