using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;
using System.Buffers;

namespace EncodingApp.Encoding.Encoders;

public class BWTEncoder : IEncoder
{
    public string DisplayName => "BWT (Преобразование Барроуза-Уилера)";
    private const string MetadataKey = "BwtOriginalIndex";

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        if (n == 1)
        {
            byte[] rentedBuffer1 = ArrayPool<byte>.Shared.Rent(1);
            rentedBuffer1[0] = inputSpan[0];
            return new EncodingResult
            {
                RentedBuffer = rentedBuffer1,
                Length = 1,
                Metadata = new Dictionary<string, object> { { MetadataKey, 0 } }
            };
        }

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(n);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int[] indices = CyclicSuffixArray.Build(inputSpan, n);

        int originalIndex = 0;

        for (int i = 0; i < n; i++)
        {
            int idx = indices[i];
            outputSpan[i] = idx == 0 ? inputSpan[n - 1] : inputSpan[idx - 1];

            if (idx == 0) originalIndex = i;
        }

        ArrayPool<int>.Shared.Return(indices);

        var metadata = new Dictionary<string, object> { { MetadataKey, originalIndex } };

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = n,
            Metadata = metadata
        };
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        if (n == 1)
        {
            byte[] rentedBuffer1 = ArrayPool<byte>.Shared.Rent(1);
            rentedBuffer1[0] = inputSpan[0];
            return new EncodingResult { RentedBuffer = rentedBuffer1, Length = 1, Metadata = null };
        }

        if (metadata == null || !metadata.TryGetValue(MetadataKey, out object? obj) || obj is not int originalIndex)
            throw new InvalidOperationException("BWT Decode: Missing BwtOriginalIndex in metadata");

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(n);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int[] transform = ArrayPool<int>.Shared.Rent(n);

        int[] count = new int[256];
        foreach (byte b in inputSpan) count[b]++;

        int sum = 0;
        for (int i = 0; i < 256; i++)
        {
            int tmp = count[i];
            count[i] = sum;
            sum += tmp;
        }

        for (int i = 0; i < n; i++)
            transform[i] = count[inputSpan[i]]++;
        

        int idx = originalIndex;
        for (int i = n - 1; i >= 0; i--)
        {
            outputSpan[i] = inputSpan[idx];
            idx = transform[idx];
        }

        ArrayPool<int>.Shared.Return(transform);

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = n,
            Metadata = null
        };
    }
   
}