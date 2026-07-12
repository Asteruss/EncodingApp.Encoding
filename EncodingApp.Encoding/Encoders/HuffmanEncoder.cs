using System.Buffers;
using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders.Helpers;

namespace EncodingApp.Encoding.Encoders;

public class HuffmanEncoder : IEncoder
{
    private const string MetadataKey = "HuffmanFreqs";
    public string DisplayName => "Кодирование Хаффмана";
    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        if (inputSpan.IsEmpty)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        // Подсчет частот
        int[] frequencies = new int[256];
        foreach (byte b in inputSpan)
            frequencies[b]++;
        

        // Построение дерева и получение кодов
        uint[] codes = new uint[256];
        int[] codeLengths = new int[256];
        BuildTreeAndGetCodes(frequencies, codes, codeLengths);

        // Упаковка в биты
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(inputSpan.Length + 1);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        foreach (byte b in inputSpan)
        {
            WriteBits(outputSpan, ref outIndex, ref bitBuffer, ref bitCount, codes[b], codeLengths[b]);
        }

        if (bitCount > 0)
            outputSpan[outIndex++] = (byte)(bitBuffer >> 24);
        

        var metadata = new Dictionary<string, object>
        {
            { MetadataKey, frequencies } 
        };

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

        if (inputSpan.IsEmpty || metadata == null || !metadata.TryGetValue(MetadataKey, out object? freqObj) || freqObj is not int[] frequencies)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        

        HuffmanNode? root = RebuildTree(frequencies);
        if (root == null) return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        // Вычисляем точный размер оригинала (сумма всех частот)
        int totalOutputLength = 0;
        foreach (int f in frequencies) totalOutputLength += f;

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(totalOutputLength);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        int outIndex = 0;
        int inIndex = 0;
        uint bitBuffer = 0;
        int bitCount = 0;

        HuffmanNode? currentNode = root;

        //  Побитовое чтение и обход дерева
        while (outIndex < totalOutputLength)
        {
            if (currentNode.IsLeaf)
            {
                outputSpan[outIndex++] = currentNode.Byte;
                currentNode = root; // Возвращаемся в корень
                continue;
            }

            // Если буфер пуст, читаем следующий байт
            if (bitCount == 0)
            {
                if (inIndex >= inputSpan.Length) break; 
                bitBuffer = (uint)inputSpan[inIndex++] << 24;
                bitCount = 8;
            }

            // Читаем старший бит
            int bit = (int)((bitBuffer >> 31) & 1);
            bitBuffer <<= 1;
            bitCount--;

            // Спускаемся по дереву (0 - лево, 1 - право)
            currentNode = bit == 0 ? currentNode.Left : currentNode.Right;
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outIndex,
            Metadata = null
        };
    }

    private void BuildTreeAndGetCodes(int[] frequencies, uint[] codes, int[] codeLengths)
    {
        var heap = new MinHeap<HuffmanNode>(256);

        for (int i = 0; i < 256; i++)
        {
            if (frequencies[i] > 0)
            {
                heap.Push(new HuffmanNode { Frequency = frequencies[i], Byte = (byte)i });
            }
        }

        // Если файл состоял из одного повторяющегося символа
        if (heap.Count == 1)
        {
            codes[heap.Peek().Byte] = 0;
            codeLengths[heap.Peek().Byte] = 1;
            return;
        }

        // Строим дерево
        while (heap.Count > 1)
        {
            var left = heap.Pop();
            var right = heap.Pop();

            var parent = new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Byte = Math.Min(left.Byte, right.Byte),
                Left = left,
                Right = right
            };
            heap.Push(parent);
        }

        // Обходим дерево и собираем битовое представление кодов
        void Traverse(HuffmanNode node, uint code, int depth)
        {
            if (node == null) return;
            if (node.IsLeaf)
            {
                codes[node.Byte] = code;
                codeLengths[node.Byte] = depth;
                return;
            }
            Traverse(node.Left, (code << 1), depth + 1);      // Лево = 0
            Traverse(node.Right, (code << 1) | 1, depth + 1);  // Право = 1
        }

        Traverse(heap.Pop(), 0, 0);
    }

    private HuffmanNode? RebuildTree(int[] frequencies)
    {
        var heap = new MinHeap<HuffmanNode>(256);
        for (int i = 0; i < 256; i++)
        {
            if (frequencies[i] > 0) heap.Push(new HuffmanNode { Frequency = frequencies[i], Byte = (byte)i });
        }

        if (heap.Count == 0) return null;
        if (heap.Count == 1) return heap.Pop();

        while (heap.Count > 1)
        {
            var left = heap.Pop();
            var right = heap.Pop();
            heap.Push(new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Byte = Math.Min(left.Byte, right.Byte),
                Left = left,
                Right = right
            });
        }
        return heap.Pop();
    }


    private static void WriteBits(Span<byte> outputSpan, ref int outIndex, ref uint bitBuffer, ref int bitCount, uint code, int length)
    {
        bitBuffer |= code << (32 - length - bitCount);
        bitCount += length;

        while (bitCount >= 8)
        {
            outputSpan[outIndex++] = (byte)(bitBuffer >> 24);
            bitBuffer <<= 8;
            bitCount -= 8;
        }
    }


}