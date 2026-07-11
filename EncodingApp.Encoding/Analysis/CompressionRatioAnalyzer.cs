using EncodingApp.Encoding.DTO;
using EncodingApp.Encoding.Core;


namespace EncodingApp.Encoding.Analysis;


public class CompressionRatioAnalyzer : IAnalyzer
{
    private readonly List<StepCompressionRecord> _records = new();
    private long _currentInputSize;

    public void OnStepStart(string stepName, ReadOnlyMemory<byte> input)
    {
        _currentInputSize = input.Length;
    }

    public void OnStepEnd(string stepName, EncodingResult output, long elapsedTicks)
    {
        double ratio = 1.0;
        double savedPercent = 0.0;

        // Защита от деления на 0, если на вход пришел пустой массив
        if (_currentInputSize > 0)
        {
            ratio = (double)output.Length / _currentInputSize;
            savedPercent = (1.0 - ratio) * 100.0;
        }

        _records.Add(new StepCompressionRecord
        {
            StepName = stepName,
            InputSizeBytes = _currentInputSize,
            OutputSizeBytes = output.Length,
            CompressionRatio = Math.Round(ratio, 4),
            SpaceSavedPercent = Math.Round(savedPercent, 2)
        });
    }

    public object GetReport()
    {
        return _records;
    }
}
