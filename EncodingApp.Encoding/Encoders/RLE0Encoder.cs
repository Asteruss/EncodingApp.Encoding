using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class RLE0Encoder : IEncoder
{
    private const string RunAKey = "Rle0RunA";
    private const string RunBKey = "Rle0RunB";
    private const string LiteralEscapeKey = "Rle0LiteralEscape";

    public string DisplayName => "RLE0";


    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        (byte runA, byte runB, byte litEsc) = FindMarkerBytes(inputSpan);

        // Первый проход: разбиваем на серии одинаковых байт и считаем точный размер выхода
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

            if (b == 0)
                outputSize += BijectiveBase2.DigitCount(runLen);
            else if (b == runA || b == runB || b == litEsc)
                outputSize += 2 * runLen; // каждое вхождение экранируется индивидуально
            else
                outputSize += runLen;

            i += runLen;
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(outputSize);
        Span<byte> outSpan = rentedBuffer.AsSpan();
        int pos = 0;

        var digitsScratch = new List<byte>(32);

        foreach (var (value, length) in runs)
        {
            if (value == 0)
            {
                digitsScratch.Clear();
                BijectiveBase2.Encode(length, digitsScratch, runA, runB);
                foreach (byte d in digitsScratch)
                    outSpan[pos++] = d;
            }
            else if (value == runA || value == runB || value == litEsc)
            {
                for (int k = 0; k < length; k++)
                {
                    outSpan[pos++] = litEsc;
                    outSpan[pos++] = value;
                }
            }
            else
            {
                for (int k = 0; k < length; k++)
                    outSpan[pos++] = value;
            }
        }

        var metadata = new Dictionary<string, object>
        {
            { RunAKey, (int)runA },
            { RunBKey, (int)runB },
            { LiteralEscapeKey, (int)litEsc }
        };

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

        if (metadata == null)
            throw new InvalidOperationException("RLE0 Decode: Missing metadata");

        byte runA = ReadByteMeta(metadata, RunAKey);
        byte runB = ReadByteMeta(metadata, RunBKey);
        byte litEsc = ReadByteMeta(metadata, LiteralEscapeKey);

        // Первый проход: вычисляем точную длину результата
        int decodedLength = 0;
        int i = 0;
        while (i < n)
        {
            byte b = inputSpan[i];
            if (b == runA || b == runB)
            {
                int start = i;
                while (i < n && (inputSpan[i] == runA || inputSpan[i] == runB))
                    i++;

                long runLength = BijectiveBase2.Decode(inputSpan.Slice(start, i - start), runA, runB);
                decodedLength += (int)runLength;
            }
            else if (b == litEsc)
            {
                if (i + 1 >= n)
                    throw new InvalidOperationException("RLE0 Decode: Truncated escape sequence");

                decodedLength += 1;
                i += 2;
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
            if (b == runA || b == runB)
            {
                int start = i;
                while (i < n && (inputSpan[i] == runA || inputSpan[i] == runB))
                    i++;

                long runLength = BijectiveBase2.Decode(inputSpan.Slice(start, i - start), runA, runB);
                for (long k = 0; k < runLength; k++)
                    outSpan[pos++] = 0;
            }
            else if (b == litEsc)
            {
                outSpan[pos++] = inputSpan[i + 1];
                i += 2;
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

    private static byte ReadByteMeta(Dictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? obj))
            throw new InvalidOperationException($"RLE0 Decode: Missing {key} in metadata");

        return obj switch
        {
            byte b => b,
            int v => (byte)v,
            _ => throw new InvalidOperationException($"RLE0 Decode: Invalid {key} metadata type")
        };
    }

    /// <summary>
    /// Выбирает 3 наименее частых байтовых значения среди 1..255 (0 исключён —
    /// им управляет структура формата, а не резервирование) под RUNA/RUNB/LIT_ESC.
    /// </summary>
    private static (byte runA, byte runB, byte litEsc) FindMarkerBytes(ReadOnlySpan<byte> input)
    {
        Span<int> counts = stackalloc int[256];
        foreach (byte b in input) counts[b]++;

        Span<int> candidates = stackalloc int[255];
        for (int v = 1; v <= 255; v++) candidates[v - 1] = v;

        byte[] picked = new byte[3];
        for (int slot = 0; slot < 3; slot++)
        {
            int bestIdx = -1;
            int bestCount = int.MaxValue;
            for (int k = 0; k < candidates.Length; k++)
            {
                int v = candidates[k];
                if (v == -1) continue; // уже выбран ранее
                if (counts[v] < bestCount)
                {
                    bestCount = counts[v];
                    bestIdx = k;
                }
            }
            picked[slot] = (byte)candidates[bestIdx];
            candidates[bestIdx] = -1;
        }

        return (picked[0], picked[1], picked[2]);
    }
}
