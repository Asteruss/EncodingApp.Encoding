using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.Core;

public static class AlgorithmRegistry
{
    private static readonly Dictionary<string, Type> _encoderTypes;

    static AlgorithmRegistry()
    {
        var assembly = typeof(IEncoder).Assembly;

        _encoderTypes = assembly.GetTypes()
            .Where(t => typeof(IEncoder).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t);
    }

    public static List<string> GetAvailableEncoders()
    {
        return _encoderTypes.Keys.OrderBy(k => k).ToList();
    }

    public static IEncoder Create(string name)
    {
        if (name != null && _encoderTypes.TryGetValue(name, out Type? type))
        {
            // Активируем экземпляр класса без параметров (у всех IEncoder пустые конструкторы)
            return (IEncoder)Activator.CreateInstance(type)!;
        }

        throw new ArgumentException($"Алгоритм '{name}' не найден в реестре.");
    }
}