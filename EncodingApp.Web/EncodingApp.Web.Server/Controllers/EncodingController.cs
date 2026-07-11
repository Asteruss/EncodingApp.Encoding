using EncodingApp.Encoding.Analysis;
using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Core.Validation;
using EncodingApp.Encoding.Formats;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EncodingApp.Web.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompressionController : ControllerBase
{
    // Внедряем анализаторы через DI
    private readonly CompositeAnalyzer _analyzers;

    public CompressionController(CompositeAnalyzer analyzers)
    {
        _analyzers = analyzers;
    }

    [HttpGet("algorithms")]
    public ActionResult GetAlgorithms()
    {
        var algorithms = AlgorithmRegistry.GetAvailableEncoders();
        var rules = ValidationRulesProvider.GetRules();

        return Ok(new { algorithms, validationRules = rules });
    }

    [HttpPost("encode")]
    public async Task<ActionResult> Encode([FromForm] IFormFileCollection files, [FromForm] string algorithms)
    {
        if (files == null || files.Count == 0) return BadRequest("Нет файлов для сжатия.");

        string[]? selectedAlgorithms;
        try
        {
            selectedAlgorithms = JsonSerializer.Deserialize<string[]>(algorithms);
            if (selectedAlgorithms == null || selectedAlgorithms.Length == 0) return BadRequest("Не выбрано алгоритмов.");
        }
        catch
        {
            return BadRequest("Неверный формат списка алгоритмов.");
        }

        var encoders = selectedAlgorithms.Select(AlgorithmRegistry.Create).ToArray();
        var pipeline = new CompressionPipeline(encoders, _analyzers);

        var results = new List<object>();

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] originalBytes = ms.ToArray();

            PipelineResult encodedResult = pipeline.ProcessEncode(originalBytes);

            byte[] cbinBytes = CbinFormatter.Pack(encodedResult, originalBytes.Length, selectedAlgorithms);
            string base64Cbin = Convert.ToBase64String(cbinBytes);

            var metrics = _analyzers.GetReport();

            results.Add(new
            {
                originalName = file.FileName,
                chain = selectedAlgorithms,
                cbinBase64 = base64Cbin,
                metrics
            });
        }

        return Ok(results);
    }

    [HttpPost("decode")]
    public async Task<ActionResult> Decode([FromForm] IFormFileCollection files)
    {
        if (files == null || files.Count == 0) return BadRequest("Нет файлов для распаковки.");

        var results = new List<object>();

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] cbinBytes = ms.ToArray();

            // Распаковываем заголовок
            CbinPackage package = CbinFormatter.Unpack(cbinBytes);

            // Динамически восстанавливаем цепочку из файла!
            var decoders = package.StepNames.Select(AlgorithmRegistry.Create).ToArray();
            var pipeline = new CompressionPipeline(decoders, _analyzers);

            // Выполняем распаковку
            PipelineResult decodedResult = pipeline.ProcessDecode(package.CompressedData, package.Metadata);

            // Конвертируем в Base64
            string base64File = Convert.ToBase64String(decodedResult.Data);

            // Простая логика для восстановления имени (отрезаем .cbin)
            string restoredName = package.OriginalSize > 0 ? file.FileName.Replace(".cbin", "_restored.cbin") : file.FileName;

            var metrics = _analyzers.GetReport();

            results.Add(new
            {
                originalName = file.FileName,
                restoredName = restoredName,
                chain = package.StepNames,
                fileBase64 = base64File,
                metrics = metrics
            });
        }

        return Ok(results);
    }
}