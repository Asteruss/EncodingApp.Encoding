using EncodingApp.Encoding.DTO;
using EncodingApp.Encoding.Core;
using System.Diagnostics;

namespace EncodingApp.Encoding.Analysis;

public class TimingAnalyzer : IAnalyzer
{
    private readonly List<StepTimingRecord> _records = new();
    private long _currentInputSize;

    public void OnStepStart(string stepName, ReadOnlyMemory<byte> input)
    {
        // Запоминаем размер входных данных, чтобы в конце шага высчитать скорость
        _currentInputSize = input.Length;
    }

    public void OnStepEnd(string stepName, EncodingResult output, long elapsedTicks)
    {
        // Конвертируем тики в миллисекунды, используя частоту процессора (Stopwatch.Frequency)
        double milliseconds = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;

        // Конвертируем тики в секунды для расчета скорости
        double seconds = (double)elapsedTicks / Stopwatch.Frequency;

        // Расчет пропускной способности (MB/s)
        double mbps = 0;
        if (seconds > 0)
        {
            double megabytesProcessed = _currentInputSize / (1024.0 * 1024.0);
            mbps = megabytesProcessed / seconds;
        }

        _records.Add(new StepTimingRecord
        {
            StepName = stepName,
            InputSizeBytes = _currentInputSize,
            OutputSizeBytes = output.Length,
            ElapsedTicks = elapsedTicks,
            ElapsedMilliseconds = Math.Round(milliseconds, 4),
            ThroughputMBps = Math.Round(mbps, 2)            
        });
    }

    public object GetReport()
    {
        return _records;
    }
}
