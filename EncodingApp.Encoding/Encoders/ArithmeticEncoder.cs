using EncodingApp.Encoding.Core;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class ArithmeticEncoder : IEncoder
{
    private const string MetadataKey = "ArithmeticFreqs";

    private const uint MSB = 0x80000000;
    private const uint SECOND_MSB = 0x40000000;
    private const uint MASK_LOWER_31 = 0x7FFFFFFF;

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        int[] originalFrequencies = new int[256];
        foreach (byte b in inputSpan) originalFrequencies[b]++;

        long[] scaledFreqs = new long[257];
        long totalFreq = ScaleFrequencies(originalFrequencies, scaledFreqs);

        uint low = 0;
        uint high = 0xFFFFFFFF;
        int pendingBits = 0;

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length + 16);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        foreach (byte symbol in inputSpan)
        {
            ulong range = (ulong)high - (ulong)low + 1;

            high = low + (uint)((range * (ulong)scaledFreqs[symbol + 1]) / (ulong)totalFreq - 1);
            low += (uint)((range * (ulong)scaledFreqs[symbol]) / (ulong)totalFreq);

            // Нормализация
            while (true)
            {
                if ((low & MSB) == (high & MSB))
                {
                    OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, (low & MSB) != 0);
                    while (pendingBits > 0)
                    {
                        OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, (low & MSB) == 0);
                        pendingBits--;
                    }
                }
                else if ((low & SECOND_MSB) != 0 && (high & SECOND_MSB) == 0)
                {
                    pendingBits++;
                    low &= ~SECOND_MSB;
                    high |= SECOND_MSB;
                }
                else break;

                low <<= 1;
                high = (high << 1) | 1;
            }
        }

        pendingBits++;
        if ((low & SECOND_MSB) != 0)
        {
            OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, true);
            while (pendingBits > 0)
            {
                OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, false);
                pendingBits--;
            }
        }
        else
        {
            OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, false);
            while (pendingBits > 0)
            {
                OutputBit(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, true);
                pendingBits--;
            }
        }

        if (bitCount > 0) outputSpan[outIndex++] = (byte)(bitBuffer >> 24);

        var metadata = new Dictionary<string, object> { { MetadataKey, originalFrequencies } };

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = metadata
        };
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        if (inputSpan.IsEmpty || metadata == null || !metadata.TryGetValue(MetadataKey, out object? freqObj) || freqObj is not int[] originalFrequencies)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        long[] scaledFreqs = new long[257];
        long totalFreq = ScaleFrequencies(originalFrequencies, scaledFreqs);

        int totalOutputLength = 0;
        foreach (int f in originalFrequencies) totalOutputLength += f;

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(totalOutputLength);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        uint low = 0;
        uint high = 0xFFFFFFFF;
        uint code = 0;

        int inIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        // Читаем первые 32 бита
        for (int i = 0; i < 32; i++)
        {
            code <<= 1;
            code |= (uint)ReadBit(inputSpan, ref inIndex, ref bitBuffer, ref bitCount);
        }

        int outIndex = 0;

        while (outIndex < totalOutputLength)
        {
            ulong range = (ulong)high - (ulong)low + 1;

            ulong scaledValue = ((ulong)(code - low) + 1) * (ulong)totalFreq - 1;
            scaledValue /= range;

            int symbol = 0;
            for (int i = 0; i < 256; i++)
            {
                if (scaledFreqs[i + 1] > (long)scaledValue)
                {
                    symbol = i;
                    break;
                }
            }

            outputSpan[outIndex++] = (byte)symbol;

            high = low + (uint)((range * (ulong)scaledFreqs[symbol + 1]) / (ulong)totalFreq - 1);
            low += (uint)((range * (ulong)scaledFreqs[symbol]) / (ulong)totalFreq);


            while (true)
            {
                if ((low & MSB) == (high & MSB))
                {
                    // Биты совпадают
                }
                else if ((low & SECOND_MSB) != 0 && (high & SECOND_MSB) == 0)
                {
                    low &= ~SECOND_MSB;
                    high |= SECOND_MSB;
                    code ^= SECOND_MSB;
                }
                else break;

                low <<= 1;
                high = (high << 1) | 1;
                code = (code << 1) | (uint)ReadBit(inputSpan, ref inIndex, ref bitBuffer, ref bitCount);
            }
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }

    private long ScaleFrequencies(int[] original, long[] scaled)
    {
        long total = 0;
        foreach (int f in original) total += f;

        // Отключаем масштабирование для файлов до ~100 МБ.
        // Это исключает любую потерю точности double при тестировании.
        if (total <= 100_000_000)
        {
            for (int i = 0; i < 256; i++) scaled[i + 1] = scaled[i] + original[i];
            return total;
        }

        const long targetTotal = 100_000_000;
        double scale = (double)targetTotal / total;
        long newTotal = 0;

        for (int i = 0; i < 256; i++)
        {
            long s = original[i] == 0 ? 0 : Math.Max(1, (long)(original[i] * scale));
            scaled[i + 1] = s;
            newTotal += s;
        }

        for (int i = 1; i <= 256; i++) scaled[i] += scaled[i - 1];

        return newTotal;
    }

    private static void OutputBit(Span<byte> output, ref int outIndex, ref uint bitBuffer, ref int bitCount, bool bit)
    {
        if (bit) bitBuffer |= (1u << (31 - bitCount));
        bitCount++;

        if (bitCount == 8)
        {
            output[outIndex++] = (byte)(bitBuffer >> 24);
            bitBuffer = 0;
            bitCount = 0;
        }
    }

    private static int ReadBit(ReadOnlySpan<byte> input, ref int inIndex, ref uint bitBuffer, ref int bitCount)
    {
        if (bitCount == 0)
        {
            bitBuffer = inIndex < input.Length ? input[inIndex++] : (uint)0;
            bitCount = 8;
        }
        bitCount--;
        return (int)((bitBuffer >> bitCount) & 1);
    }
}