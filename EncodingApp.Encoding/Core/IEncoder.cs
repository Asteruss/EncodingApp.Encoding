namespace EncodingApp.Encoding.Core;

public interface IEncoder
{
    public EncodingResult Encode(ReadOnlyMemory<byte> input);
    public EncodingResult Decode(ReadOnlyMemory<byte> input, Dictionary<string, object>? metadata);
}
