namespace EncodingApp.Encoding.DTO;

public class StepTimingRecord
{
    public string StepName { get; set; } = string.Empty;

    public long InputSizeBytes { get; set; }
    public long OutputSizeBytes { get; set; }

    public long ElapsedTicks { get; set; }
    public double ElapsedMilliseconds { get; set; }

    // Пропускная способность
    public double ThroughputMBps { get; set; }
}
