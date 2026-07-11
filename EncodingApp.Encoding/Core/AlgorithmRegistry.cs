using EncodingApp.Encoding.DTO;
namespace EncodingApp.Encoding.Core;

public static class AlgorithmRegistry
{
    private static readonly Dictionary<string, Type> _encoderTypes;
    private static readonly List<EncoderInfo> _encoderInfos;

    static AlgorithmRegistry()
    {
        var assembly = typeof(IEncoder).Assembly;

        _encoderTypes = assembly.GetTypes()
            .Where(t => typeof(IEncoder).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t);

        // ИЗМЕНЕНО: Создаем экземпляры один раз при старте, чтобы вытащить имена
        _encoderInfos = _encoderTypes.Select(kvp =>
        {
            var instance = (IEncoder)Activator.CreateInstance(kvp.Value)!;
            return new EncoderInfo
            {
                Id = kvp.Key,
                DisplayName = instance.DisplayName
            };
        }).OrderBy(info => info.DisplayName).ToList(); // Сортируем по русскому алфавиту!
    }

    /// <summary>
    /// Возвращает список алгоритмов с их русскими названиями для отрисовки в UI.
    /// </summary>
    public static List<EncoderInfo> GetAvailableEncoders()
    {
        return _encoderInfos;
    }

    public static IEncoder Create(string name)
    {
        if (name != null && _encoderTypes.TryGetValue(name, out Type? type))
        {
            return (IEncoder)Activator.CreateInstance(type)!;
        }
        throw new ArgumentException($"Алгоритм '{name}' не найден в реестре.");
    }
}