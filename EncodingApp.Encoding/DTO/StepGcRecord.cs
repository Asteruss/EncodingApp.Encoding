namespace EncodingApp.Encoding.DTO;

public class StepGcRecord
{
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Сколько байт было выделено в управляемой куче во время выполнения шага
    /// </summary>
    public long AllocatedBytesDelta { get; set; }

    /// <summary>
    /// Сколько раз сработал GC поколения 0 (самый быстрый)
    /// </summary>
    public int Gen0CollectionsDelta { get; set; }

    /// <summary>
    /// Сколько раз сработал GC поколения 1
    /// </summary>
    public int Gen1CollectionsDelta { get; set; }

    /// <summary>
    /// Сколько раз сработал GC поколения 2 (самый медленный, Stop-The-World).
    /// Если это число > 0 при сжатии, значит алгоритм создает крупные объекты в куче.
    /// </summary>
    public int Gen2CollectionsDelta { get; set; }
}