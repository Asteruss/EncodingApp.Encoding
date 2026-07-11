using EncodingApp.Encoding.DTO;

namespace EncodingApp.Encoding.Core.Validation;

public static class ValidationRulesProvider
{
    public static List<ValidationRule> GetRules()
    {
        var rules = new List<ValidationRule>();

        var names = AlgorithmRegistry.GetAvailableEncoders()
            .ToDictionary(k => k.Id, v => v.DisplayName);

        string FormatNames(string[] ids) => string.Join(" и ", ids.Select(id => names.TryGetValue(id, out var n) ? n : id));

        var transformers = new[] { "BWTEncoder", "MTFEncoder", "DeltaEncoder" };
        var entropy = new[] { "HuffmanEncoder", "ArithmeticEncoder" };
        var dictionary = new[] { "LZ77Encoder", "LZWEncoder" };

        foreach (var t in transformers)
        {
            rules.Add(new ValidationRule
            {
                Target = t,
                Type = "RequiresAnyOf",
                RequiredIds = CombineArrays(entropy, new[] { "RLEEncoder" }),
                Message = $"{names[t]} не сжимает данные сам по себе. Добавьте {FormatNames(entropy)} или RLE."
            });
        }

        rules.Add(new ValidationRule
        {
            Target = "MTFEncoder",
            Type = "PreferredAfter",
            RelatedIds = new[] { "BWTEncoder" },
            Message = $"Совет: {names["MTFEncoder"]} работает значительно эффективнее сразу после {names["BWTEncoder"]}."
        });

        rules.Add(new ValidationRule
        {
            Target = "BWTEncoder",
            Type = "IncompatibleWith",
            IncompatibleIds = dictionary,
            Message = $"Применение {FormatNames(dictionary)} после {names["BWTEncoder"]} неэффективно, BWT уничтожает словарные совпадения."
        });

        foreach (var d in dictionary)
        {
            rules.Add(new ValidationRule
            {
                Target = d,
                Type = "PreferredFirst",
                Message = $"{names[d]} лучше работает в начале цепочки (до блоковых трансформаций)."
            });
        }

        rules.Add(new ValidationRule
        {
            Target = "RLEEncoder",
            Type = "WarnIfAlone",
            Message = $"RLE на текстах или случайных данных может увеличить размер в 2 раза."
        });

        return rules;
    }

    private static string[] CombineArrays(params string[][] arrays)
    {
        var result = new List<string>();
        foreach (var arr in arrays) result.AddRange(arr);
        return result.ToArray();
    }
}
