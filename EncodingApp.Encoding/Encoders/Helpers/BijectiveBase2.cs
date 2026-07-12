namespace EncodingApp.Encoding.Encoders.Helpers;

public static class BijectiveBase2
{
    public static int DigitCount(long value)
    {
        int count = 0;
        while (value > 0)
        {
            value -= 1;
            value /= 2;
            count++;
        }
        return count;
    }

    public static void Encode(long value, List<byte> digitsOut, byte runA, byte runB)
    {
        while (value > 0)
        {
            value -= 1;
            int digit = (int)(value % 2) + 1; // 1 or 2
            digitsOut.Add(digit == 1 ? runA : runB);
            value /= 2;
        }
    }

    public static long Decode(ReadOnlySpan<byte> digits, byte runA, byte runB)
    {
        long value = 0;
        long placeValue = 1;
        for (int i = 0; i < digits.Length; i++)
        {
            int digit = digits[i] == runA ? 1 : 2;
            value += digit * placeValue;
            placeValue *= 2;
        }
        return value;
    }
}
