using EncodingApp.Encoding.Core;

namespace EncodingApp.Encoding.Analysis;

public class CompositeAnalyzer : IAnalyzer
{
    private readonly Dictionary<string, IAnalyzer> _analyzers;

    /// <summary>
    /// Создает композит из массива анализаторов. 
    /// Ключи для отчета генерируются автоматически на основе имени класса
    /// </summary>
    public CompositeAnalyzer(params IAnalyzer[] analyzers)
    {
        _analyzers = new Dictionary<string, IAnalyzer>();
        if (analyzers == null) return;

        foreach (var analyzer in analyzers)
        {
            if (analyzer == null) continue;

            // Генерируем базовый ключ
            string baseKey = analyzer.GetType().Name;
            if (baseKey.EndsWith("Analyzer"))
                baseKey = baseKey.Substring(0, baseKey.Length - "Analyzer".Length);
            
            if (baseKey.Length > 0)
                baseKey = char.ToLowerInvariant(baseKey[0]) + baseKey.Substring(1);
            

            string finalKey = baseKey;
            int suffix = 1;
            while (_analyzers.ContainsKey(finalKey))
                finalKey = $"{baseKey}_{suffix++}";
            

            _analyzers[finalKey] = analyzer;
        }
    }

    /// <summary>
    /// Создает композит с явно заданными ключами для отчета.
    /// </summary>
    public CompositeAnalyzer(Dictionary<string, IAnalyzer> analyzers)
    {
        _analyzers = analyzers ?? [];
    }

    public void OnStepStart(string stepName, ReadOnlyMemory<byte> input)
    {
        // Делегируем вызов всем вложенным анализаторам
        foreach (var analyzer in _analyzers.Values)
        {
            analyzer.OnStepStart(stepName, input);
        }
    }

    public void OnStepEnd(string stepName, EncodingResult output, long elapsedTicks)
    {
        foreach (var analyzer in _analyzers.Values)
            analyzer.OnStepEnd(stepName, output, elapsedTicks);
        
    }

    public object GetReport()
    {
        var combinedReport = new Dictionary<string, object>();

        foreach (var kvp in _analyzers) 
            combinedReport[kvp.Key] = kvp.Value.GetReport();
        
        return combinedReport;
    }
}