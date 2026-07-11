using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.Core;

public readonly struct EncodingResult
{
    public byte[]? RentedBuffer { get; init; }
    public ReadOnlyMemory<byte> Data => RentedBuffer?.AsMemory(0, Length) ?? ReadOnlyMemory<byte>.Empty;
    public int Length { get; init; }  
    public Dictionary<string, object>? Metadata { get; init; }
}
