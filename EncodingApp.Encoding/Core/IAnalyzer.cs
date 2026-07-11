namespace EncodingApp.Encoding.Core;

public interface IAnalyzer
{
    /// <summary>
    /// Вызывается перед выполнением шага кодирования/декодирования.
    /// </summary>
    /// <param name="stepName">Имя алгоритма (например, "BWT" или "RLE")</param>
    /// <param name="input">Входящие данные шага</param>
    void OnStepStart(string stepName, ReadOnlyMemory<byte> input);

    /// <summary>
    /// Вызывается после выполнения шага.
    /// </summary>
    /// <param name="stepName">Имя алгоритма</param>
    /// <param name="output">Результат работы алгоритма</param>
    /// <param name="elapsedTicks">Время выполнения в тиках Stopwatch</param>
    void OnStepEnd(string stepName, EncodingResult output, long elapsedTicks);

    /// <summary>
    /// Вызывается после завершения всего конвейера, отдает отчет
    /// </summary>
    object GetReport();
}