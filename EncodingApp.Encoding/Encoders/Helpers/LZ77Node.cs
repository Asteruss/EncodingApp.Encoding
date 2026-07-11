using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.Encoders.Helpers;

public readonly struct LZ77Node
{
    public int Offset { get; }
    public int Length { get; }
    public byte Next { get; }

    public LZ77Node(int offset, int length, byte next)
    {
        Offset = offset;
        Length = length;
        Next = next;
    }
}