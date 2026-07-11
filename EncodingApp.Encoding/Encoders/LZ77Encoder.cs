using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;
public class LZ77Encoder : IEncoder
{
    private const int WindowSize = 32768;
    private const int MaxMatchLength = 255;
    public string DisplayName => "LZ77";

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length * 4);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int pos = 0;
        int outIndex = 0;

        while (pos < inputSpan.Length)
        {
            // Ищем в уже просмотренных данных (от 0 до pos)
            var (offset, length) = FindMatching(inputSpan, pos);
            // Берем следующий символ (он всегда есть, так как мы резервируем 1 байт в FindMatching)
            byte nextSymbol = inputSpan[pos + length];
            // Формируем узел
            var node = new LZ77Node(offset, length, nextSymbol);
            // Пишем 3 значения из узла в выходной поток
            WriteToken(outputSpan, ref outIndex, node);
            // Сдвигаем позицию (перепрыгиваем совпадение + 1 литерал)
            pos += length + 1;
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }

    private static (int Offset, int Length) FindMatching(ReadOnlySpan<byte> input, int currentPos)
    {
        int bestOffset = int.MaxValue;
        int bestLength = 0;

        int maxLength = Math.Min(255, input.Length - currentPos - 1);
        if (maxLength <= 0) return (0, 0);

        int searchStart = Math.Max(0, currentPos - WindowSize);  

        for (int i = searchStart; i < currentPos; i++)
        {
            int currentLength = 0;
            while (currentLength < maxLength &&
                   input[i + currentLength] == input[currentPos + currentLength])
            {
                currentLength++;
            }

            int currentOffset = currentPos - i;

            if (currentLength > 0 && (currentLength > bestLength ||
               (currentLength == bestLength && currentOffset < bestOffset)))
            {
                bestLength = currentLength;
                bestOffset = currentOffset;
            }
        }

        return (bestOffset == int.MaxValue ? 0 : bestOffset, bestLength);
    }


    private static void WriteToken(Span<byte> outputSpan, ref int outIndex, LZ77Node node)
    {
        outputSpan[outIndex++] = (byte)(node.Offset);
        outputSpan[outIndex++] = (byte)(node.Offset >> 8);

        outputSpan[outIndex++] = (byte)node.Length;

        outputSpan[outIndex++] = node.Next;
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty || inputSpan.Length % 4 != 0)
        {
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        }

        int maxPossibleOutputSize = (inputSpan.Length / 4) * (MaxMatchLength + 1);
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxPossibleOutputSize);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;

        for (int i = 0; i < inputSpan.Length; i += 4)
        {
            int distance = inputSpan[i] | (inputSpan[i + 1] << 8);
            int length = inputSpan[i + 2];
            byte literal = inputSpan[i + 3];

            if (distance > 0 && length > 0)
            {
                if (distance > outIndex)
                    throw new InvalidDataException($"LZ77 Error: Invalid distance {distance} at pos {outIndex}");

                int matchStartIndex = outIndex - distance;
                for (int j = 0; j < length; j++)
                    outputSpan[outIndex++] = outputSpan[matchStartIndex + j];
                
            }

            outputSpan[outIndex++] = literal;
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }
}