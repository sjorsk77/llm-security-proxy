using Dapper;
using Microsoft.ML;
using Microsoft.ML.Data;
using Shared;
using Shared.settings;

namespace llm_protector.filters;

public class PromptInput
{
    public string PromptText { get; set; } = string.Empty;
    [ColumnName("Label")] public bool IsInjection { get; set; }
}

public class PromptPrediction
{
    [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
    [ColumnName("Probability")] public float Probability { get; set; }
    [ColumnName("Score")] public float Score { get; set; }
}


public class TfIdfFilter
{
    private readonly DatabaseService _db;
    private readonly SettingsService _settingsService;
    private PredictionEngine<PromptInput, PromptPrediction>? _predictionEngine;

    public TfIdfFilter(DatabaseService db)
    {
        _db = db;
        var mlContext = new MLContext();
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(PromptInput.PromptText))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label",
                featureColumnName: "Features"));

        var trainingData = GetTrainingData();
        var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

        var model = pipeline.Fit(dataView);
        _predictionEngine = mlContext.Model.CreatePredictionEngine<PromptInput, PromptPrediction>(model);

    }

    public float GetInjectionProbability(string message)
    {
        if (_predictionEngine == null) return 0f;

        var prediction = _predictionEngine.Predict(new PromptInput { PromptText = message });
        return prediction.Probability;
    }

    private List<PromptInput> GetTrainingData()
    {
        using var conn = _db.CreateConnection();
        return conn.Query<PromptInput>("SELECT PromptText, IsInjection FROM PromptTrainingData").ToList();
    }
}