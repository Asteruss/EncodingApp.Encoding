using System.Buffers;
using EncodingApp.Encoding.Core;

namespace EncodingApp.Encoding.Encoders;

public class MTFEncoder : IEncoder
{
    public string DisplayName => "MTF";

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        byte[] list = new byte[256];
        for (int i = 0; i < 256; i++) list[i] = (byte)i;

        int outIndex = 0;

        foreach (byte symbol in inputSpan)
        {
            // Ищем индекс символа (Линейный поиск по 256 элементам невероятно быстр в L1 кэше)
            int index = 0;
            while (list[index] != symbol)
            {
                index++;
            }

            outputSpan[outIndex++] = (byte)index;

            // Сдвигаем элементы влево, освобождая место для символа на позиции 0
            if (index > 0)
            {
                Array.Copy(list, 0, list, 1, index);
                list[0] = symbol;
            }
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
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

        byte[] list = new byte[256];
        for (int i = 0; i < 256; i++) list[i] = (byte)i;

        int outIndex = 0;

        foreach (byte index in inputSpan)
        {
            byte symbol = list[index];
            outputSpan[outIndex++] = symbol;

            if (index > 0)
            {
                Array.Copy(list, 0, list, 1, index);
                list[0] = symbol;
            }
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }
}