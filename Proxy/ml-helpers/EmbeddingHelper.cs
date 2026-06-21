using llm_protector.ml;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Onnx;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace llm_protector.ml_helpers;

public class EmbeddingHelper
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;

    public EmbeddingHelper()
    {
        string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "embedding", "model.onnx");
        string vocabPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "embedding", "vocab.txt");
        var builder = Kernel.CreateBuilder();
        
        builder.AddBertOnnxEmbeddingGenerator(modelPath, vocabPath);
        
        var kernel = builder.Build();
        
        _embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }
    
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var result = await _embeddingGenerator.GenerateAsync(new[] { text });
        
        return result[0].Vector.ToArray();
    }
}