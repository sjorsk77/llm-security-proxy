
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace llm_protector.filters;

public class OnnxFilter
{
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;

    public OnnxFilter()
    {
        string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "transformer");
        string spmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "transformer", "spm.model");

        _session = new InferenceSession(Path.Combine(modelPath, "model.onnx"));
        using var stream = File.OpenRead(spmPath);
        
        _tokenizer = SentencePieceTokenizer.Create(stream);
    }

    public float GetInjectionProbability(string message)
    {
        const int maxLen = 512;
        var ids = _tokenizer.EncodeToIds(message);
        
        long[] inputIds = new long[maxLen];
        long[] maskArray = new long[maxLen];
        
        for (int i = 0; i < maxLen; i++)
        {
            if (i < ids.Count)
            {
                inputIds[i] = (long)ids[i];
                maskArray[i] = 1;
            }
            else
            {
                inputIds[i] = 0;
                maskArray[i] = 0;
            }
        }
        
        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, maxLen });
        var maskTensor = new DenseTensor<long>(maskArray, new[] { 1, maxLen });
        
        using var results = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        });
 
        var output = results.First().AsTensor<float>();
        
        float injectionLogit = output[0, 1];
    
        return Sigmoid(injectionLogit);
    }
    
    private float Sigmoid(float x)
    {
        return (float)(1.0 / (1.0 + Math.Exp(-x)));
    }
}