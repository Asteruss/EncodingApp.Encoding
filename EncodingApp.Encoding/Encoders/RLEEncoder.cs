using EncodingApp.Encoding.Core;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class RLEEncoder : IEncoder
{
    public string DisplayName => "RLE (Кодирование длин серий)";
    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty)
        {
            return new EncodingResult
            {
                RentedBuffer = null,
                Length = 0,
                Metadata = null
            };
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length * 2 + 1);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        byte currentByte = inputSpan[0];
        int count = 1;

        for (int i = 1; i < inputSpan.Length; i++)
        {
            if (inputSpan[i] == currentByte && count < 255)
            {
                count++;
            }
            else
            {
                outputSpan[outIndex++] = currentByte;
                outputSpan[outIndex++] = (byte)count;

                currentByte = inputSpan[i];
                count = 1;
            }
        }

        outputSpan[outIndex++] = currentByte;
        outputSpan[outIndex++] = (byte)count;


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

        if (inputSpan.IsEmpty || inputSpan.Length % 2 != 0)
        {
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        }

        int maxOutputSize = 0;
        for (int i = 1; i < inputSpan.Length; i += 2)
            maxOutputSize += inputSpan[i];
        

        if (maxOutputSize == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxOutputSize);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        for (int i = 0; i < inputSpan.Length; i += 2)
        {
            byte value = inputSpan[i];     
            byte count = inputSpan[i + 1]; 

            outputSpan.Slice(outIndex, count).Fill(value);
            outIndex += count;
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }
}