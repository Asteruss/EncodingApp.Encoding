namespace EncodingApp.Encoding.Encoders.Helpers;

class HuffmanNode : IComparable<HuffmanNode>
{
    public int Frequency { get; set; }
    public byte Byte { get; set; }
    public HuffmanNode? Left { get; set; }
    public HuffmanNode? Right { get; set; }
    public bool IsLeaf => Left == null && Right == null;

    public int CompareTo(HuffmanNode? other)
    {
        if (other == null) return 1;
        int cmp = Frequency.CompareTo(other.Frequency);
        if (cmp != 0) return cmp;
        return Byte.CompareTo(other.Byte);
    }
}

