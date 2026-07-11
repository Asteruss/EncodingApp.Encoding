using EncodingApp.Encoding.Core;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class DeltaEncoder : IEncoder
{
    public string DisplayName => "Delta";

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        // Delta не меняет размер данных (1 байт = 1 байт)
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        outputSpan[0] = inputSpan[0];

        for (int i = 1; i < inputSpan.Length; i++)
        {
            outputSpan[i] = (byte)(inputSpan[i] - inputSpan[i - 1]);
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = inputSpan.Length,
            Metadata = null 
        };
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        // Восстанавливаем первый символ
        outputSpan[0] = inputSpan[0];

        for (int i = 1; i < inputSpan.Length; i++)
            outputSpan[i] = (byte)(outputSpan[i - 1] + inputSpan[i]);
        

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = inputSpan.Length,
            Metadata = null
        };
    }
}