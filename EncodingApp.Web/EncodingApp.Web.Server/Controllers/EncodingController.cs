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
            selectedAlgorithms = algorithms?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (selectedAlgorithms == null || selectedAlgorithms.Length == 0) return BadRequest("Не выбрано алгоритмов.");
        }
        catch
        {
            return BadRequest("Неверный формат списка алгоритмов. Ожидается: RLE, MTF, Huffman");
        }

        var encoders = selectedAlgorithms.Select(AlgorithmRegistry.Create).ToArray();
        var results = new List<object>();

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] originalBytes = ms.ToArray();

            var localAnalyzers = new CompositeAnalyzer(
                new TimingAnalyzer(),
                new CompressionRatioAnalyzer(),
                new GcPressureAnalyzer()
            );

            var pipeline = new CompressionPipeline(encoders, localAnalyzers);

            PipelineResult encodedResult = pipeline.ProcessEncode(originalBytes);
            byte[] cbinBytes = CbinFormatter.Pack(encodedResult, originalBytes.Length, file.FileName, selectedAlgorithms);
            string base64Cbin = Convert.ToBase64String(cbinBytes);

            var metrics = localAnalyzers.GetReport();

            results.Add(new
            {
                originalName = file.FileName,
                chain = selectedAlgorithms,
                base64Cbin,
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
            try
            {
                using var stream = file.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] cbinBytes = ms.ToArray();

                CbinPackage package = CbinFormatter.Unpack(cbinBytes);
                var decoders = package.StepNames.Select(name => AlgorithmRegistry.Create(name)).ToArray();
                var localAnalyzers = new CompositeAnalyzer(new TimingAnalyzer(), new CompressionRatioAnalyzer());
                var pipeline = new CompressionPipeline(decoders, localAnalyzers);

                PipelineResult decodedResult = pipeline.ProcessDecode(package.CompressedData, package.Metadata);

                string base64File = Convert.ToBase64String(decodedResult.Data.ToArray());

                string restoredName = file.FileName.Replace(".cbin", package.OriginalExtension);

                var metrics = localAnalyzers.GetReport();

                results.Add(new
                {
                    originalName = file.FileName,
                    restoredName,
                    chain = string.Join(" -> ", package.StepNames),
                    base64File,
                    metrics
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка распаковки {file.FileName}: {ex.Message}");
            }
        }

        return Ok(results);
    }
}