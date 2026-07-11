using EncodingApp.Encoding.Core;
using SystemEncoding = System.Text.Encoding;

namespace EncodingApp.Encoding.Formats;

public static class CbinFormatter
{
    private const string Magic = "ENC";
    private const byte Version = 1;
    private const byte MetaTypeInt = 1;
    private const byte MetaTypeIntArray = 2;

    // ИЗМЕНЕНО: Принимаем PipelineResult
    public static byte[] Pack(PipelineResult pipelineResult, int originalSize, string[] stepNames)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, SystemEncoding.UTF8, leaveOpen: true);

        writer.Write(SystemEncoding.ASCII.GetBytes(Magic));
        writer.Write(Version);
        writer.Write(originalSize);

        writer.Write((ushort)stepNames.Length);
        foreach (var name in stepNames)
        {
            byte[] nameBytes = SystemEncoding.UTF8.GetBytes(name);
            writer.Write((byte)nameBytes.Length);
            writer.Write(nameBytes);
        }

        var meta = pipelineResult.Metadata;
        int metaCount = meta?.Count ?? 0;
        writer.Write((ushort)metaCount);

        if (meta != null)
        {
            foreach (var kvp in meta)
            {
                byte[] keyBytes = SystemEncoding.UTF8.GetBytes(kvp.Key);
                writer.Write((byte)keyBytes.Length);
                writer.Write(keyBytes);

                if (kvp.Value is int intVal) { writer.Write(MetaTypeInt); writer.Write(intVal); }
                else if (kvp.Value is int[] arrVal) { writer.Write(MetaTypeIntArray); writer.Write(arrVal.Length); foreach (int v in arrVal) writer.Write(v); }
                else { writer.Write((byte)0); }
            }
        }

        writer.Write(pipelineResult.Data);

        return ms.ToArray();
    }

    public static CbinPackage Unpack(ReadOnlyMemory<byte> fileBytes)
    {
        using var ms = new MemoryStream(fileBytes.ToArray());
        using var reader = new BinaryReader(ms, SystemEncoding.UTF8, leaveOpen: true);

        byte[] magicBuf = reader.ReadBytes(3);
        if (SystemEncoding.ASCII.GetString(magicBuf) != Magic) throw new InvalidDataException("Неверный формат файла");

        byte version = reader.ReadByte();
        if (version != Version) throw new InvalidDataException($"Неподдерживаемая версия: {version}");

        int originalSize = reader.ReadInt32();

        int stepsCount = reader.ReadUInt16();
        string[] stepNames = new string[stepsCount];
        for (int i = 0; i < stepsCount; i++) stepNames[i] = SystemEncoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));

        var metadata = new Dictionary<string, object>();
        int metaCount = reader.ReadUInt16();
        for (int i = 0; i < metaCount; i++)
        {
            string key = SystemEncoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
            byte type = reader.ReadByte();
            if (type == MetaTypeInt) metadata[key] = reader.ReadInt32();
            else if (type == MetaTypeIntArray) { int len = reader.ReadInt32(); int[] arr = new int[len]; for (int j = 0; j < len; j++) arr[j] = reader.ReadInt32(); metadata[key] = arr; }
        }

        ReadOnlyMemory<byte> compressedData = fileBytes.Slice((int)ms.Position);
        return new CbinPackage(originalSize, stepNames, metadata, compressedData);
    }
}

public readonly struct CbinPackage
{
    public int OriginalSize { get; }
    public string[] StepNames { get; }
    public Dictionary<string, object> Metadata { get; }
    public ReadOnlyMemory<byte> CompressedData { get; }
    public CbinPackage(int originalSize, string[] stepNames, Dictionary<string, object> metadata, ReadOnlyMemory<byte> compressedData)
    {
        OriginalSize = originalSize; StepNames = stepNames; Metadata = metadata; CompressedData = compressedData;
    }
}