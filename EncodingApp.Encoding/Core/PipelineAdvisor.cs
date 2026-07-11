namespace EncodingApp.Encoding.Core;

public static class PipelineAdvisor
{
    /// <summary>
    /// Проверяет цепочку и возвращает список предупреждений.
    /// Если список пуст - цепочка оптимальна.
    /// </summary>
    public static List<string> AnalyzeSequence(string[] selectedSteps)
    {
        var warnings = new List<string>();

        if (selectedSteps == null || selectedSteps.Length == 0)
        {
            warnings.Add("Не выбрано ни одного алгоритма сжатия.");
            return warnings;
        }

        bool hasBwt = selectedSteps.Contains("BWTEncoder");
        bool hasMtf = selectedSteps.Contains("MTFEncoder");
        bool hasEntropy = selectedSteps.Any(s => s == "HuffmanEncoder" || s == "ArithmeticEncoder");
        bool hasDictionary = selectedSteps.Any(s => s == "LZ77Encoder" || s == "LZWEncoder");
        bool hasRle = selectedSteps.Contains("RLEEncoder");
        bool hasDelta = selectedSteps.Contains("DeltaEncoder");

        // Правило 1: Трансформаторы без энтропийного кодера
        if ((hasBwt || hasMtf || hasDelta) && !hasEntropy && !hasRle)
        {
            warnings.Add("Внимание: BWT, MTF и Delta не сжимают данные сами по себе. " +
                         "Их смысл в подготовке данных для энтропийных кодеров (Huffman/Arithmetic) или RLE.");
        }

        // Правило 2: BWT без MTF
        if (hasBwt && !hasMtf)
        {
            warnings.Add("Совет: BWT работает значительно эффективнее, если сразу после него идет MTF (Move-to-Front). " +
                         "Без MTF вы теряете до 30% потенциала сжатия.");
        }

        // Правило 3: Конфликт словарных и блоковых трансформаций
        if (hasBwt && hasDictionary)
        {
            warnings.Add("Необычная комбинация: BWT обычно используется с MTF и Huffman. " +
                         "Применение LZ77/LZW после BWT может быть неэффективным, так как BWT уничтожает словарные совпадения.");
        }

        // Правило 4: Энтропийное кодирование перед словарным (неправильный порядок)
        int huffIndex = Array.IndexOf(selectedSteps, "HuffmanEncoder");
        int lzIndex = Array.IndexOf(selectedSteps, "LZ77Encoder");
        int lzwIndex = Array.IndexOf(selectedSteps, "LZWEncoder");

        if (huffIndex != -1 && (lzIndex != -1 || lzwIndex != -1))
        {
            int dictIndex = lzIndex != -1 ? lzIndex : lzwIndex;
            if (huffIndex < dictIndex)
            {
                warnings.Add("Подозрительный порядок: Энтропийное кодирование (Huffman) стоит перед словарным (LZ77/LZW). " +
                             "Обычно словарные алгоритмы идут первыми.");
            }
        }

        // Правило 5: Одиночный RLE на случайных данных
        if (selectedSteps.Length == 1 && hasRle)
        {
            warnings.Add("RLE сам по себе хорошо работает только для данных с длинными повторениями (например, графики). " +
                         "На текстах или уже сжатых файлах он может увеличить размер в 2 раза.");
        }

        return warnings;
    }
}