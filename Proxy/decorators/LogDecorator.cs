using llm_protector.protection;
using Shared.settings;

namespace llm_protector.decorators;

public enum LogCategory
{
    DECODING,
    STATIC,
    MACHINE_LEARNING
}

public enum MachineLearningType
{
    TF_IDF,
    EMBEDDING,
    TRANSFORMER
}

public enum StaticFilterType
{
    WORD,
    PATTERN
}

public class LogDecorator(ILogger<LogDecorator> _logger)
{
    public void LogDecodingStep(DecodingMethod decodingMethod, long duration, bool changed)
    {
        _logger.LogInformation("DecodingMethod: {DecodingMethod}, Duration: {Duration}, Changed: {Changed}",
            decodingMethod, duration, changed);
    }

    public void LogDecodingProcess(bool changed, long duration, int iterations)
    {
        _logger.LogInformation("Category: {Category}, Changed: {Changed}, Duration: {Duration}, Iterations: {Iterations}", 
            LogCategory.DECODING, changed, duration, iterations);
    }

    public void LogStaticFilterProcess(long duration, bool found)
    {
        _logger.LogInformation("Category: {Category}, Duration: {Duration}, Found: {Found}", 
            LogCategory.STATIC, duration, found);
    }
    
    public void LogPatternFound(long duration, string pattern, string fileName, int iterations)
    {
        _logger.LogInformation("Category: {Category}, Duration: {Duration}, MatchedPattern: {MatchedPattern}, File: {FileName}, Iterations: {Iterations}", 
            LogCategory.STATIC, duration, pattern, fileName, iterations);
    }
    
    public void LogMachineLearningStep(MachineLearningType machineLearningType, long duration)
    {
        _logger.LogInformation("MachineLearningType: {MachineLearningType}, Duration: {Duration}", 
            machineLearningType, duration);
    }
    
    public void MachineLearningProcess(long duration)
    {
        _logger.LogInformation("Category: {Category}, Duration: {Duration}", 
            LogCategory.MACHINE_LEARNING, duration);
    }

    public void LogBlocked(string reason)
    {
        _logger.LogInformation("Blocked: {Reason}", reason);
    }
}