using Microsoft.ML.Tokenizers;

namespace llm_protector.ml;

public class TokenizerHelper
{
    private readonly SentencePieceTokenizer _tokenizer;

    public TokenizerHelper()
    {
        /*string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string spmModelPath = Path.Combine(baseDir, "onnx", "spm.model");
        
        using var stream = File.OpenRead(spmModelPath);
        _tokenizer = SentencePieceTokenizer.Create(stream);*/
    }

    public (long[] ids, long[] mask) Tokenize(string prompt)
    {
        var ids = _tokenizer.EncodeToIds(prompt).ToList();

        ids.Insert(0, 1);
        ids.Add(2);

        int sequenceLength = 512;
        long[] paddedIds = new long[sequenceLength];
        long[] attentionMask = new long[sequenceLength];

        for (int i = 0; i < sequenceLength; i++)
        {
            if (i < ids.Count)
            {
                paddedIds[i] = ids[i];
                attentionMask[i] = 1;
            }
            else
            {
                paddedIds[i] = 0;
                attentionMask[i] = 0;
            }
        }
        
        return (paddedIds, attentionMask);
    }
}