using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace llm_protector.ml;

public class OnnxPromptAnalyzer
{
    private readonly InferenceSession _session;
    private readonly TokenizerHelper _tokenizer;

    public OnnxPromptAnalyzer(TokenizerHelper tokenizer)
    {
        /*_tokenizer = tokenizer;
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string onnxPath = Path.Combine(baseDir, "onnx", "m.onnx");
        string spmModelPath = Path.Combine(baseDir, "onnx", "spm.model");
        
        _session = new InferenceSession(onnxPath);*/
    }

    public float AnalyzePromptEmbedded(string prompt)
    {
        var (ids, mask) = _tokenizer.Tokenize(prompt);

        var inputTensor = new DenseTensor<long>(ids, new[] { 1, 512 });
        var maskTensor = new DenseTensor<long>(mask, new[] { 1, 512 });
        
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor),
        };
        
        using var results = _session.Run(inputs);
        var score = results.First().AsEnumerable<float>().ToArray();
        
        return Sigmoid(score[1]);
    }

    private float Sigmoid(float x)
    {
        return (float)(1.0 / (1.0 + Math.Exp(-x)));
    }
}