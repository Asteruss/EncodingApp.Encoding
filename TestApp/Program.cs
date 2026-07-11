using EncodingApp.Encoding.Analysis;
using EncodingApp.Encoding.Core;
using EncodingApp.Encoding.Encoders;
using EncodingApp.Encoding.Formats;
using System.Text.Json;

//string inputFilePath = @"C:\Users\Artem\Desktop\вуз\учебная практика 2 курс\pics\milp_gq9s_210621.jpg";
string inputFilePath = @"C:\Users\Artem\Desktop\вуз\учебная практика 2 курс\pics\ЮАЮ.png";

string compressedFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath) ?? "", Path.GetFileNameWithoutExtension(inputFilePath) + ".cbin");
string decompressedFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath) ?? "", Path.GetFileNameWithoutExtension(inputFilePath) + "_restored" + Path.GetExtension(inputFilePath));

if (!File.Exists(inputFilePath)) { Console.WriteLine($"Файл не найден: {inputFilePath}"); return; }

byte[] originalBytes = File.ReadAllBytes(inputFilePath);
Console.WriteLine($"Исходный файл: {inputFilePath} ({originalBytes.Length} байт)");

var analyzers = new CompositeAnalyzer(new TimingAnalyzer(), new CompressionRatioAnalyzer());
//var encoders = new IEncoder[] { new RLEEncoderWithEspaceByte() };
var encoders = new IEncoder[] { new BWTEncoder(), new MTFEncoder(), new HuffmanEncoder() };
//var encoders = new IEncoder[] { new ArithmeticEncoder()};
var encodePipeline = new CompressionPipeline(encoders, analyzers);

Console.WriteLine("Сжатие...");
PipelineResult encodedResult = encodePipeline.ProcessEncode(originalBytes);

// Формируем .cbin файл. Никакого управления памятью!
string[] stepNames = encoders.Select(e => e.GetType().Name).ToArray();
byte[] cbinFileBytes = CbinFormatter.Pack(encodedResult, originalBytes.Length, stepNames);
File.WriteAllBytes(compressedFilePath, cbinFileBytes);

Console.WriteLine($"Сжатый файл: {compressedFilePath} ({cbinFileBytes.Length} байт)");
Console.WriteLine("Метрики:\n" + JsonSerializer.Serialize(analyzers.GetReport(), new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("\nРаспаковка...");
CbinPackage package = CbinFormatter.Unpack(File.ReadAllBytes(compressedFilePath));
Console.WriteLine($"Извлечена цепочка из файла: {string.Join(" -> ", package.StepNames)}");

var decoders = package.StepNames.Select(name => AlgorithmFactory.Create(name)).ToArray();
var decodePipeline = new CompressionPipeline(decoders);


PipelineResult decodedResult = decodePipeline.ProcessDecode(package.CompressedData, package.Metadata);

File.WriteAllBytes(decompressedFilePath, decodedResult.Data);
Console.WriteLine($"Распакованный файл: {decompressedFilePath}");

if (originalBytes.SequenceEqual(decodedResult.Data))
    Console.WriteLine("✅ УСПЕХ: Файлы идентичны.");
else
    Console.WriteLine("❌ ОШИБКА: Данные повреждены.");

Console.ReadKey();

public static class AlgorithmFactory
{
    public static IEncoder Create(string name) => name switch
    {
        "RLEEncoder" => new RLEEncoder(),
        "BWTEncoder" => new BWTEncoder(),
        "MTFEncoder" => new MTFEncoder(),
        "LZ77Encoder" => new LZ77Encoder(),
        "LZWEncoder" => new LZWEncoder(),
        "HuffmanEncoder" => new HuffmanEncoder(),
        "ArithmeticEncoder" => new ArithmeticEncoder(),
        "DeltaEncoder" => new DeltaEncoder(),
        "RLEEncoderWithEspaceByte" => new RLEEncoderWithEspaceByte()
    };
}