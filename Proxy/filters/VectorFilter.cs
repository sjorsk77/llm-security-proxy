namespace llm_protector.filters;

public class VectorFilter
{
    private readonly List<(string Name, float[] Vector)> _jailbreakDatabase = [];
    private readonly int _cunkSize = 30;
    private readonly int _overlap = 10;
    
    //todo: create the vector db

    public async Task<float> CalculateSimilarity(string message, float similarityThreshold = 0.9f)
    {
        var segments = GetMessageSegments(message);
        var maxSimilarity = 0f;

        foreach (var segment in segments)
        {
            float[] segmentVector = await GenerateEmbedding(segment);

            foreach (var attack in _jailbreakDatabase)
            {
                float similarity = CalculateCosineSimilarity(segmentVector, attack.Vector);
                if (similarity > similarityThreshold) return similarity;
                if (similarity > maxSimilarity) maxSimilarity = similarity;
            }
        }
        
        return maxSimilarity;
    }
    
    private float CalculateCosineSimilarity(float[] vecA, float[] vecB)
    {
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < vecA.Length; i++)
        {
            dot += vecA[i] * vecB[i];
            magA += vecA[i] * vecA[i];
            magB += vecB[i] * vecB[i];
        }
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
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

    private async Task<float[]> GenerateEmbedding(string text)
    {
        return await Task.FromResult(new float[384]);
    }
}