using EncodingApp.Encoding.Analysis;
using System.Buffers;
using System.Diagnostics;

namespace EncodingApp.Encoding.Core;

public class CompressionPipeline
{
    private readonly List<IEncoder> _steps;
    private readonly IAnalyzer? _analyzer;

    public CompressionPipeline(IEnumerable<IEncoder> steps, IAnalyzer? analyzer = null)
    {
        if (steps == null) throw new ArgumentNullException(nameof(steps));
        _steps = steps.ToList();
        if (_steps.Count == 0) throw new ArgumentException("Pipeline должен содержать хотя бы один шаг.", nameof(steps));
        _analyzer = analyzer;
    }

    public PipelineResult ProcessEncode(ReadOnlyMemory<byte> inputData)
    {
        EncodingResult currentResult = RentInputCopy(inputData);
        var aggregatedMetadata = new Dictionary<string, object>();

        try
        {
            foreach (var step in _steps)
            {
                string stepName = step.GetType().Name + "_Encode";
                ProcessStep(step, stepName, ref currentResult);

                if (currentResult.Metadata != null)
                {
                    foreach (var kvp in currentResult.Metadata) aggregatedMetadata[kvp.Key] = kvp.Value;
                }
            }

            var finalInternalResult = new EncodingResult
            {
                RentedBuffer = currentResult.RentedBuffer,
                Length = currentResult.Length,
                Metadata = aggregatedMetadata.Count > 0 ? aggregatedMetadata : null
            };
            return new PipelineResult(finalInternalResult);
        }
        catch
        {
            if (currentResult.RentedBuffer != null) ArrayPool<byte>.Shared.Return(currentResult.RentedBuffer);
            throw;
        }
    }

    public PipelineResult ProcessDecode(ReadOnlyMemory<byte> encodedData, Dictionary<string, object>? metadata)
    {
        EncodingResult currentResult = RentInputCopy(encodedData);

        try
        {
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                var step = _steps[i];
                string stepName = step.GetType().Name + "_Decode";

                _analyzer?.OnStepStart(stepName, currentResult.Data);
                var sw = Stopwatch.StartNew();
                var nextResult = step.Decode(currentResult.Data, metadata);
                sw.Stop();
                _analyzer?.OnStepEnd(stepName, nextResult, sw.ElapsedTicks);

                if (currentResult.RentedBuffer != null) ArrayPool<byte>.Shared.Return(currentResult.RentedBuffer);
                currentResult = nextResult;
            }

            return new PipelineResult(currentResult); // Буфер вернется в пул внутри DTO
        }
        catch
        {
            if (currentResult.RentedBuffer != null) ArrayPool<byte>.Shared.Return(currentResult.RentedBuffer);
            throw;
        }
    }

    private void ProcessStep(IEncoder step, string stepName, ref EncodingResult currentResult)
    {
        ReadOnlyMemory<byte> inputForStep = currentResult.Data;
        _analyzer?.OnStepStart(stepName, inputForStep);
        var sw = Stopwatch.StartNew();
        var nextResult = step.Encode(inputForStep);
        sw.Stop();
        _analyzer?.OnStepEnd(stepName, nextResult, sw.ElapsedTicks);
        if (currentResult.RentedBuffer != null) ArrayPool<byte>.Shared.Return(currentResult.RentedBuffer);
        currentResult = nextResult;
    }

    private EncodingResult RentInputCopy(ReadOnlyMemory<byte> input)
    {
        if (input.IsEmpty) return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(input.Length);
        input.Span.CopyTo(rentedBuffer);
        return new EncodingResult { RentedBuffer = rentedBuffer, Length = input.Length, Metadata = null };
    }
}