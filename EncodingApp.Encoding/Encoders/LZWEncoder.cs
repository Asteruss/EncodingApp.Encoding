using System.Buffers;
using EncodingApp.Encoding.Core;

namespace EncodingApp.Encoding.Encoders;

public class LZWEncoder : IEncoder
{
    private const int Bits = 14;
    private const int MaxValue = (1 << Bits) - 1;   
    private const int MaxCode = MaxValue - 1;        
    private const int MaxStackLength = 8192;
    public string DisplayName => "LZW";


    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        int maxOutputSize = (inputSpan.Length * Bits / 8) + 1024;
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxOutputSize);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        var dict = new Dictionary<int, int>(MaxCode);
        int nextCode = 256;

        int outIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        int stringCode = inputSpan[0];

        for (int i = 1; i < inputSpan.Length; i++)
        {
            int character = inputSpan[i];
            int key = (stringCode << 8) | character;

            if (dict.TryGetValue(key, out int newCode))
            {
                stringCode = newCode;
            }
            else
            {
                WriteCode(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, stringCode);

                if (nextCode <= MaxCode)
                    dict.Add(key, nextCode++);
                
                stringCode = character;
            }
        }

        WriteCode(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, stringCode);
        WriteCode(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, MaxValue); // EOF

        if (bitCount > 0)
            outputSpan[outIndex++] = (byte)(bitBuffer >> 24);
        

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

        int maxOutputSize = (inputSpan.Length * 8 / Bits) * 3;
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxOutputSize);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int[] prefixCodes = new int[MaxCode + 1];
        byte[] appendChars = new byte[MaxCode + 1];
        int nextCode = 256;

        int outIndex = 0;
        int inIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        Span<byte> decodeStack = stackalloc byte[MaxStackLength];

        int oldCode = ReadCode(inputSpan, ref inIndex, ref bitBuffer, ref bitCount);

        if (oldCode == MaxValue)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        }

        int character = oldCode;
        outputSpan[outIndex++] = (byte)oldCode;

        int newCode;
        while ((newCode = ReadCode(inputSpan, ref inIndex, ref bitBuffer, ref bitCount)) != MaxValue)
        {
            int currentCode = newCode;  
            int stackPtr = 0;

            if (newCode >= nextCode)
            {
                decodeStack[stackPtr++] = (byte)character;
                newCode = oldCode;
            }

            while (newCode > 255)
            {
                decodeStack[stackPtr++] = appendChars[newCode];
                newCode = prefixCodes[newCode];
            }

            decodeStack[stackPtr++] = (byte)newCode;
            character = newCode;

            while (stackPtr > 0)
                outputSpan[outIndex++] = decodeStack[--stackPtr];

            if (nextCode <= MaxCode)
            {
                prefixCodes[nextCode] = oldCode;
                appendChars[nextCode] = (byte)character;
                nextCode++;
            }

            oldCode = currentCode;  
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }

    private static void WriteCode(Span<byte> outputSpan, ref int outIndex, ref uint bitBuffer, ref int bitCount, int code)
    {
        bitBuffer |= (uint)code << (32 - Bits - bitCount);
        bitCount += Bits;

        while (bitCount >= 8)
        {
            outputSpan[outIndex++] = (byte)(bitBuffer >> 24);
            bitBuffer <<= 8;
            bitCount -= 8;
        }
    }

    private static int ReadCode(ReadOnlySpan<byte> inputSpan, ref int inIndex, ref uint bitBuffer, ref int bitCount)
    {
        while (bitCount <= 24 && inIndex < inputSpan.Length)
        {
            bitBuffer |= (uint)inputSpan[inIndex++] << (24 - bitCount);
            bitCount += 8;
        }

        if (bitCount < Bits)
            return MaxValue; 

        int code = (int)(bitBuffer >> (32 - Bits));
        bitBuffer <<= Bits;
        bitCount -= Bits;

        return code;
    }
}