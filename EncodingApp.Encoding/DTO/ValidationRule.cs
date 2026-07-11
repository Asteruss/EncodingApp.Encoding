using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.DTO;

public class ValidationRule
{
    public string Target { get; set; } = string.Empty; // К какому алгоритму относится правило
    public string Type { get; set; } = string.Empty;   // Тип правила (ключ для switch/case на фронте)
    public string Message { get; set; } = string.Empty; // Текст предупреждения

    public string[]? RequiredIds { get; set; }       // Для типа "RequiresAnyOf" (С кем обязательно в связке)
    public string[]? RelatedIds { get; set; }        // Для типа "PreferredAfter" (Кто должен быть перед ним)
    public string[]? IncompatibleIds { get; set; }   // Для типа "IncompatibleWith" (С кем конфликтует)
}
