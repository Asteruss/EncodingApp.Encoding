namespace EncodingApp.Encoding.DTO;

public class StepCompressionRecord
{
    public string StepName { get; set; } = string.Empty;
    public long InputSizeBytes { get; set; }
    public long OutputSizeBytes { get; set; }

    /// <summary>
    /// Коэффициент сжатия
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Процент сэкономленного места (или потерянного, если отрицательный)
    /// </summary>
    public double SpaceSavedPercent { get; set; }
}
