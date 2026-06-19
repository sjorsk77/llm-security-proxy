using llm_protector.ml;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace llm_protector.ml_helpers;

public class EmbeddingHelper
{
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;

    public EmbeddingHelper()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string onnxPath = Path.Combine(baseDir, "onnx", "model_optimized.onnx");
        string spmPath = Path.Combine(baseDir, "onnx", "spm.model");
        
        var options = new SessionOptions();
        options.IntraOpNumThreads = 4;
        options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
        
        _session = new InferenceSession(onnxPath, options);
        using var stream = File.OpenRead(spmPath);
        _tokenizer = SentencePieceTokenizer.Create(stream);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        return await Task.Run(() =>
        {
            var (ids, mask) = Tokenize(text);

            var inputTensor = new DenseTensor<long>(ids, new[] { 1, 512 });
            var maskTensor = new DenseTensor<long>(mask, new[] { 1, 512 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
            };

            using var results = _session.Run(inputs);
        
            return results.First().AsEnumerable<float>().Take(384).ToArray();
        });
    }

    private (long[] ids, long[] mask) Tokenize(string text)
    {
        var ids = _tokenizer.EncodeToIds(text).ToList();
        ids.Insert(0, 1);
        ids.Add(2);

        int seqLen = 512;
        long[] paddedIds = new long[seqLen];
        long[] mask = new long[seqLen];
        
        for (int i = 0; i < seqLen; i++)
        {
            paddedIds[i] = i < ids.Count ? (long)ids[i] : 0;
            mask[i] = i < ids.Count ? 1 : 0;
        }
        
        return (paddedIds, mask);
    }
}