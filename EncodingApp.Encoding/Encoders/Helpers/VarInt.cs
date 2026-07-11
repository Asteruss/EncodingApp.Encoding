namespace EncodingApp.Encoding.Encoders.Helpers;

public static class VarInt
{
    public static int VarUIntSize(uint value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }
        return size;
    }

    public static int WriteVarUInt(Span<byte> dest, uint value)
    {
        int pos = 0;
        while (value >= 0x80)
        {
            dest[pos++] = (byte)(value | 0x80);
            value >>= 7;
        }
        dest[pos++] = (byte)value;
        return pos;
    }

    public static uint ReadVarUInt(ReadOnlySpan<byte> src, out int bytesRead)
    {
        uint result = 0;
        int shift = 0;
        int i = 0;

        while (true)
        {
            if (i >= src.Length)
            {
                bytesRead = 0;
                return 0;
            }

            byte b = src[i];
            result |= (uint)(b & 0x7F) << shift;
            i++;

            if ((b & 0x80) == 0)
            {
                bytesRead = i;
                return result;
            }

            shift += 7;
            if (shift >= 35)
            {
                bytesRead = 0;
                return 0;
            }
        }
    }
}