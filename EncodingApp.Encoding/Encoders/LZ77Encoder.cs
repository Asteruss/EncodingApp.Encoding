using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class LZ77Encoder : IEncoder
{
    private const string MetadataKey = "Lz77OriginalLength";

    private const int WindowSize = 32768;
    private const int MaxMatchLength = 255;
    private const int MinMatchLength = 3;
    private const int HashBits = 15;
    private const int HashSize = 1 << HashBits;
    private const int MaxChainLength = 64;

    public string DisplayName => "Алгоритм Лемпеля-Зива-Велча(LZ77)";

    private readonly struct Token
    {
        public readonly bool IsMatch;
        public readonly byte Literal;
        public readonly int Offset;
        public readonly int Length;

        public Token(byte literal)
        {
            IsMatch = false;
            Literal = literal;
            Offset = 0;
            Length = 0;
        }

        public Token(int offset, int length)
        {
            IsMatch = true;
            Literal = 0;
            Offset = offset;
            Length = length;
        }
    }

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        int[] head = ArrayPool<int>.Shared.Rent(HashSize);
        int[] prev = ArrayPool<int>.Shared.Rent(n);
        Array.Fill(head, -1, 0, HashSize);

        var tokens = new List<Token>();
        int pos = 0;

        while (pos < n)
        {
            var (offset, length) = FindMatching(inputSpan, pos, head, prev);

            InsertHash(inputSpan, pos, head, prev, n);

            if (length >= MinMatchLength)
            {
                tokens.Add(new Token(offset, length));

                for (int skip = 1; skip < length; skip++)
                {
                    int skipPos = pos + skip;
                    if (skipPos < n)
                        InsertHash(inputSpan, skipPos, head, prev, n);
                }

                pos += length;
            }
            else
            {
                tokens.Add(new Token(inputSpan[pos]));
                pos += 1;
            }
        }

        ArrayPool<int>.Shared.Return(head);
        ArrayPool<int>.Shared.Return(prev);

        // Считаем точный размер выхода: 1 control-байт на каждые 8 токенов + полезная нагрузка
        int outputSize = 0;
        for (int i = 0; i < tokens.Count; i += 8)
        {
            outputSize += 1; // control byte
            int bitsUsed = Math.Min(8, tokens.Count - i);
            for (int b = 0; b < bitsUsed; b++)
                outputSize += tokens[i + b].IsMatch ? 3 : 1;
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(outputSize);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        int ti = 0;
        while (ti < tokens.Count)
        {
            int controlBytePos = outIndex++;
            byte control = 0;
            int bitsUsed = Math.Min(8, tokens.Count - ti);

            for (int b = 0; b < bitsUsed; b++)
            {
                Token token = tokens[ti + b];
                if (token.IsMatch)
                {
                    control |= (byte)(1 << b);
                    outputSpan[outIndex++] = (byte)(token.Offset);
                    outputSpan[outIndex++] = (byte)(token.Offset >> 8);
                    outputSpan[outIndex++] = (byte)token.Length;
                }
                else
                {
                    outputSpan[outIndex++] = token.Literal;
                }
            }

            outputSpan[controlBytePos] = control;
            ti += bitsUsed;
        }

        var metadata = new Dictionary<string, object> { { MetadataKey, n } };

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = metadata
        };
    }

    private static int Hash3(ReadOnlySpan<byte> data, int pos)
    {
        return ((data[pos] << 10) ^ (data[pos + 1] << 5) ^ data[pos + 2]) & (HashSize - 1);
    }

    private static void InsertHash(ReadOnlySpan<byte> input, int pos, int[] head, int[] prev, int n)
    {
        if (pos + 2 >= n) return;

        int h = Hash3(input, pos);
        prev[pos] = head[h];
        head[h] = pos;
    }

    private static (int Offset, int Length) FindMatching(
        ReadOnlySpan<byte> input, int currentPos, int[] head, int[] prev)
    {
        int bestOffset = 0;
        int bestLength = 0;

        int maxLength = Math.Min(MaxMatchLength, input.Length - currentPos);
        if (maxLength <= 0) return (0, 0);

        if (currentPos + 2 >= input.Length)
            return (0, 0);

        int h = Hash3(input, currentPos);
        int candidate = head[h];
        int chainLength = 0;

        while (candidate != -1 && chainLength < MaxChainLength)
        {
            int currentOffset = currentPos - candidate;
            if (currentOffset > WindowSize)
                break;

            int currentLength = 0;
            while (currentLength < maxLength &&
                   input[candidate + currentLength] == input[currentPos + currentLength])
            {
                currentLength++;
            }

            if (currentLength > bestLength ||
               (currentLength == bestLength && currentOffset < bestOffset))
            {
                bestLength = currentLength;
                bestOffset = currentOffset;

                if (bestLength >= maxLength)
                    break;
            }

            candidate = prev[candidate];
            chainLength++;
        }

        return (bestOffset, bestLength);
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        if (metadata == null || !metadata.TryGetValue(MetadataKey, out object? obj))
            throw new InvalidOperationException("LZ77 Decode: Missing Lz77OriginalLength in metadata");

        int originalLength = obj switch
        {
            int v => v,
            _ => throw new InvalidOperationException("LZ77 Decode: Invalid Lz77OriginalLength metadata type")
        };

        if (originalLength == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(originalLength);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        int readPos = 0;

        while (outIndex < originalLength)
        {
            if (readPos >= inputSpan.Length)
                throw new InvalidDataException("LZ77 Decode: Truncated stream (missing control byte)");

            byte control = inputSpan[readPos++];

            for (int b = 0; b < 8 && outIndex < originalLength; b++)
            {
                bool isMatch = (control & (1 << b)) != 0;

                if (isMatch)
                {
                    if (readPos + 2 >= inputSpan.Length)
                        throw new InvalidDataException("LZ77 Decode: Truncated match token");

                    int offset = inputSpan[readPos] | (inputSpan[readPos + 1] << 8);
                    int length = inputSpan[readPos + 2];
                    readPos += 3;

                    if (offset <= 0 || offset > outIndex)
                        throw new InvalidDataException($"LZ77 Decode: Invalid offset {offset} at pos {outIndex}");

                    int matchStartIndex = outIndex - offset;
                    for (int j = 0; j < length; j++)
                        outputSpan[outIndex++] = outputSpan[matchStartIndex + j];
                }
                else
                {
                    if (readPos >= inputSpan.Length)
                        throw new InvalidDataException("LZ77 Decode: Truncated literal token");

                    outputSpan[outIndex++] = inputSpan[readPos++];
                }
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