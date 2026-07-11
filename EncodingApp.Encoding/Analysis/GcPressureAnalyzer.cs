using EncodingApp.Encoding.DTO;
using EncodingApp.Encoding.Core;

namespace EncodingApp.Encoding.Analysis;


public class GcPressureAnalyzer : IAnalyzer
{
    private readonly List<StepGcRecord> _records = new();

    private long _startAllocatedBytes;
    private int _startGen0;
    private int _startGen1;
    private int _startGen2;

    public void OnStepStart(string stepName, ReadOnlyMemory<byte> input)
    {
        _startAllocatedBytes = GC.GetTotalAllocatedBytes(true);
        _startGen0 = GC.CollectionCount(0);
        _startGen1 = GC.CollectionCount(1);
        _startGen2 = GC.CollectionCount(2);
    }

    public void OnStepEnd(string stepName, EncodingResult output, long elapsedTicks)
    {
        long endAllocatedBytes = GC.GetTotalAllocatedBytes(true);
        int endGen0 = GC.CollectionCount(0);
        int endGen1 = GC.CollectionCount(1);
        int endGen2 = GC.CollectionCount(2);

        _records.Add(new StepGcRecord
        {
            StepName = stepName,
            AllocatedBytesDelta = endAllocatedBytes - _startAllocatedBytes,
            Gen0CollectionsDelta = endGen0 - _startGen0,
            Gen1CollectionsDelta = endGen1 - _startGen1,
            Gen2CollectionsDelta = endGen2 - _startGen2
        });
    }

    public object GetReport()
    {
        return _records;
    }
}
