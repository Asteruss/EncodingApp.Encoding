using System.Buffers;
using System.Buffers.Binary;
using EncodingApp.Encoding.Core;

namespace EncodingApp.Encoding.Encoders;


public class DCTEncoder
{
    private const int BlockSize = 8;

    private const string KeyWidth = "DCTWidth";
    private const string KeyHeight = "DCTHeight";
    private const string KeyPaddedWidth = "DCTPaddedWidth";
    private const string KeyPaddedHeight = "DCTPaddedHeight";
    private const string KeyOriginalLength = "DCTOriginalLength";
    private const string KeyQuality = "DCTQuality";

    public string DisplayName => "DCT кодирование";

    private readonly int _width;
    private readonly int _quality;

    public DCTEncoder() : this(8, 90) { }

    public DCTEncoder(int width = 8, int quality = 90)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Ширина изображения должна быть положительной.");

        _width = width;
        _quality = Math.Clamp(quality, 1, 100);
    }

    public EncodingResult Encode(ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        int n = inputSpan.Length;

        if (n == 0)
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };

        int height = (n + _width - 1) / _width;
        int paddedWidth = RoundUpToBlock(_width);
        int paddedHeight = RoundUpToBlock(height);

        int[,] quantTable = BuildQuantTable(_quality);

        int outputByteLength = paddedWidth * paddedHeight * 2; // 2 байта (short) на коэффициент
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(outputByteLength);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        double[,] block = new double[BlockSize, BlockSize];
        int outPos = 0;

        for (int blockRow = 0; blockRow < paddedHeight; blockRow += BlockSize)
        {
            for (int blockCol = 0; blockCol < paddedWidth; blockCol += BlockSize)
            {
                // Заполняем блок исходными байтами (0 за пределами реальных данных - паддинг)
                // и сразу применяем level shift (-128), как в JPEG.
                for (int r = 0; r < BlockSize; r++)
                {
                    int srcRow = blockRow + r;
                    for (int c = 0; c < BlockSize; c++)
                    {
                        int srcCol = blockCol + c;
                        byte value = 0;
                        if (srcRow < height && srcCol < _width)
                        {
                            int idx = srcRow * _width + srcCol;
                            if (idx < n) value = inputSpan[idx];
                        }
                        block[r, c] = value - 128.0;
                    }
                }

                ApplyDct2D(block);

                for (int r = 0; r < BlockSize; r++)
                {
                    for (int c = 0; c < BlockSize; c++)
                    {
                        int quant = (int)Math.Round(block[r, c] / quantTable[r, c], MidpointRounding.AwayFromZero);
                        short q = (short)Math.Clamp(quant, short.MinValue, short.MaxValue);
                        BinaryPrimitives.WriteInt16LittleEndian(outputSpan.Slice(outPos, 2), q);
                        outPos += 2;
                    }
                }
            }
        }

        var metadata = new Dictionary<string, object>
        {
            { KeyWidth, _width },
            { KeyHeight, height },
            { KeyPaddedWidth, paddedWidth },
            { KeyPaddedHeight, paddedHeight },
            { KeyOriginalLength, n },
            { KeyQuality, _quality }
        };

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = outPos,
            Metadata = metadata
        };
    }

    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;

        if (inputSpan.IsEmpty
            || metadata == null
            || !metadata.TryGetValue(KeyWidth, out object? wObj) || wObj is not int width
            || !metadata.TryGetValue(KeyHeight, out object? hObj) || hObj is not int height
            || !metadata.TryGetValue(KeyPaddedWidth, out object? pwObj) || pwObj is not int paddedWidth
            || !metadata.TryGetValue(KeyPaddedHeight, out object? phObj) || phObj is not int paddedHeight
            || !metadata.TryGetValue(KeyOriginalLength, out object? nObj) || nObj is not int n
            || !metadata.TryGetValue(KeyQuality, out object? qObj) || qObj is not int quality)
        {
            return new EncodingResult { RentedBuffer = null, Length = 0, Metadata = null };
        }

        int[,] quantTable = BuildQuantTable(quality);

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(n);
        Span<byte> outputSpan = rentedBuffer.AsSpan();

        double[,] block = new double[BlockSize, BlockSize];
        int inPos = 0;

        for (int blockRow = 0; blockRow < paddedHeight; blockRow += BlockSize)
        {
            for (int blockCol = 0; blockCol < paddedWidth; blockCol += BlockSize)
            {
                for (int r = 0; r < BlockSize; r++)
                {
                    for (int c = 0; c < BlockSize; c++)
                    {
                        short q = BinaryPrimitives.ReadInt16LittleEndian(inputSpan.Slice(inPos, 2));
                        inPos += 2;
                        block[r, c] = q * (double)quantTable[r, c];
                    }
                }

                ApplyIdct2D(block);

                for (int r = 0; r < BlockSize; r++)
                {
                    int srcRow = blockRow + r;
                    if (srcRow >= height) continue;

                    for (int c = 0; c < BlockSize; c++)
                    {
                        int srcCol = blockCol + c;
                        if (srcCol >= width) continue;

                        int idx = srcRow * width + srcCol;
                        if (idx >= n) continue;

                        int value = (int)Math.Round(block[r, c] + 128.0, MidpointRounding.AwayFromZero);
                        outputSpan[idx] = (byte)Math.Clamp(value, 0, 255);
                    }
                }
            }
        }

        return new EncodingResult
        {
            RentedBuffer = rentedBuffer,
            Length = n,
            Metadata = null
        };
    }

    // ====================================================================
    // Квантование (JPEG-style, стандартная luma-таблица + масштаб по quality)
    // ====================================================================

    private static readonly int[,] BaseLumaQuantTable = new int[8, 8]
    {
        { 16, 11, 10, 16, 24, 40, 51, 61 },
        { 12, 12, 14, 19, 26, 58, 60, 55 },
        { 14, 13, 16, 24, 40, 57, 69, 56 },
        { 14, 17, 22, 29, 51, 87, 80, 62 },
        { 18, 22, 37, 56, 68, 109, 103, 77 },
        { 24, 35, 55, 64, 81, 104, 113, 92 },
        { 49, 64, 78, 87, 103, 121, 120, 101 },
        { 72, 92, 95, 98, 112, 100, 103, 99 }
    };

    private static int[,] BuildQuantTable(int quality)
    {
        quality = Math.Clamp(quality, 1, 100);
        int scale = quality < 50 ? 5000 / quality : 200 - quality * 2;

        var table = new int[8, 8];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                int v = (BaseLumaQuantTable[i, j] * scale + 50) / 100;
                table[i, j] = Math.Clamp(v, 1, 255); // никогда не 0 - иначе деление на 0 при квантовании
            }
        }
        return table;
    }

    private static int RoundUpToBlock(int value) => ((value + BlockSize - 1) / BlockSize) * BlockSize;

    // ====================================================================
    // Ортонормированный DCT-II / DCT-III (1D) и их 2D-версии через сепарабельность
    // (2D DCT = DCT по строкам, затем DCT по столбцам; порядок неважен - оси независимы)
    // ====================================================================

    private static readonly double[] Alpha = BuildAlpha();
    private static readonly double[,] Cos = BuildCosTable();

    private static double[] BuildAlpha()
    {
        var a = new double[BlockSize];
        a[0] = Math.Sqrt(1.0 / BlockSize);
        for (int k = 1; k < BlockSize; k++) a[k] = Math.Sqrt(2.0 / BlockSize);
        return a;
    }

    private static double[,] BuildCosTable()
    {
        var cos = new double[BlockSize, BlockSize];
        for (int k = 0; k < BlockSize; k++)
            for (int n = 0; n < BlockSize; n++)
                cos[k, n] = Math.Cos(Math.PI * (2 * n + 1) * k / (2.0 * BlockSize));
        return cos;
    }

    private static void Dct1D(Span<double> vec)
    {
        Span<double> outp = stackalloc double[BlockSize];
        for (int k = 0; k < BlockSize; k++)
        {
            double sum = 0;
            for (int n = 0; n < BlockSize; n++) sum += vec[n] * Cos[k, n];
            outp[k] = Alpha[k] * sum;
        }
        outp.CopyTo(vec);
    }

    private static void Idct1D(Span<double> vec)
    {
        Span<double> outp = stackalloc double[BlockSize];
        for (int n = 0; n < BlockSize; n++)
        {
            double sum = 0;
            for (int k = 0; k < BlockSize; k++) sum += Alpha[k] * vec[k] * Cos[k, n];
            outp[n] = sum;
        }
        outp.CopyTo(vec);
    }

    private static void ApplyDct2D(double[,] block)
    {
        Span<double> line = stackalloc double[BlockSize];

        for (int r = 0; r < BlockSize; r++)
        {
            for (int c = 0; c < BlockSize; c++) line[c] = block[r, c];
            Dct1D(line);
            for (int c = 0; c < BlockSize; c++) block[r, c] = line[c];
        }

        for (int c = 0; c < BlockSize; c++)
        {
            for (int r = 0; r < BlockSize; r++) line[r] = block[r, c];
            Dct1D(line);
            for (int r = 0; r < BlockSize; r++) block[r, c] = line[r];
        }
    }

    private static void ApplyIdct2D(double[,] block)
    {
        Span<double> line = stackalloc double[BlockSize];

        for (int c = 0; c < BlockSize; c++)
        {
            for (int r = 0; r < BlockSize; r++) line[r] = block[r, c];
            Idct1D(line);
            for (int r = 0; r < BlockSize; r++) block[r, c] = line[r];
        }

        for (int r = 0; r < BlockSize; r++)
        {
            for (int c = 0; c < BlockSize; c++) line[c] = block[r, c];
            Idct1D(line);
            for (int c = 0; c < BlockSize; c++) block[r, c] = line[c];
        }
    }
}