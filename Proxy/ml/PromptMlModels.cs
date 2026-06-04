using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.ML.Data;

namespace llm_protector.ml;

public class PromptInput
{
    [LoadColumn(0)] 
    public string PromptText { get; set; } = string.Empty;
    [LoadColumn(1), ColumnName("Label")]
    public bool IsInjection { get; set; }
}

public class PromptPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }
}