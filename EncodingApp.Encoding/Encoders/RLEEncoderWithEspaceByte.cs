using System.Buffers;
using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;

namespace EncodingApp.Encoding.Encoders;

public class RLEEncoderWithEspaceByte : IEncoder
{
    private const string MetadataKey = "RleEscapeByte";
    private const int MinRunLength = 4; // ниже этого порога escape-кодирование не выгодно

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        byte escapeByte = FindLeastFrequentByte(inputSpan);

        // Первый проход: находим серии и точный размер выхода
        var runs = new List<(byte value, int length)>();
        int outputSize = 0;

        int i = 0;
        while (i < n)
        {
            byte b = inputSpan[i];
            int runLen = 1;
            while (i + runLen < n && inputSpan[i + runLen] == b)
                runLen++;

            runs.Add((b, runLen));

            if (b == escapeByte || runLen >= MinRunLength)
                outputSize += 2 + VarInt.VarUIntSize((uint)runLen); // ESCAPE + byte + varint(len)
            else
                outputSize += runLen; // литералы без изменений

            i += runLen;
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(outputSize);
        Span<byte> outSpan = rentedBuffer.AsSpan();

        int pos = 0;
        foreach (var (value, length) in runs)
        {
            if (value == escapeByte || length >= MinRunLength)
            {
                outSpan[pos++] = escapeByte;
                outSpan[pos++] = value;
                pos += VarInt.WriteVarUInt(outSpan.Slice(pos), (uint)length);
            }
            else
            {
                for (int k = 0; k < length; k++)
                    outSpan[pos++] = value;
            }
        }

        var metadata = new Dictionary<string, object> { { MetadataKey, (int)escapeByte } }; 


        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outputSize,
            Metadata = metadata
        };
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        if (metadata == null || !metadata.TryGetValue(MetadataKey, out object? obj))
            throw new InvalidOperationException("RLE Decode: Missing RleEscapeByte in metadata");

        byte escapeByte = obj switch
        {
            byte bb => bb,
            int ii => (byte)ii,
            _ => throw new InvalidOperationException("RLE Decode: Invalid RleEscapeByte metadata type")
        };

        // Первый проход: считаем итоговую длину без записи данных
        int decodedLength = 0;
        int i = 0;
        while (i < n)
        {
            byte b = inputSpan[i];
            if (b == escapeByte)
            {
                if (i + 1 >= n)
                    throw new InvalidOperationException("RLE Decode: Truncated stream (missing run byte)");

                i += 2;
                uint runLen = VarInt.ReadVarUInt(inputSpan.Slice(i), out int bytesRead);
                if (bytesRead == 0)
                    throw new InvalidOperationException("RLE Decode: Truncated stream (invalid varint)");

                decodedLength += (int)runLen;
                i += bytesRead;
            }
            else
            {
                decodedLength += 1;
                i += 1;
            }
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(decodedLength);
        Span<byte> outSpan = rentedBuffer.AsSpan();

        int pos = 0;
        i = 0;
        while (i < n)
        {
            byte b = inputSpan[i];
            if (b == escapeByte)
            {
                byte value = inputSpan[i + 1];
                i += 2;
                uint runLen = VarInt.ReadVarUInt(inputSpan.Slice(i), out int bytesRead);
                i += bytesRead;

                for (uint k = 0; k < runLen; k++)
                    outSpan[pos++] = value;
            }
            else
            {
                outSpan[pos++] = b;
                i += 1;
            }
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = decodedLength,
            Metadata = null
        };
    }

    private static byte FindLeastFrequentByte(ReadOnlySpan<byte> input)
    {
        Span<int> counts = stackalloc int[256];
        foreach (byte b in input) counts[b]++;

        byte best = 0;
        int bestCount = counts[0];
        for (int v = 1; v < 256; v++)
        {
            if (counts[v] < bestCount)
            {
                bestCount = counts[v];
                best = (byte)v;
            }
        }
        return best;
    }
}