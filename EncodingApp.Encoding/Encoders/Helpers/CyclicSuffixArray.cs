using System.Buffers;

namespace EncodingApp.Encoding.Encoders.Helpers;

public static class CyclicSuffixArray
{
    public static int[] Build(ReadOnlySpan<byte> s, int n)
    {
        int[] sa = ArrayPool<int>.Shared.Rent(n);
        int[] rank = ArrayPool<int>.Shared.Rent(n);
        int[] tmp = ArrayPool<int>.Shared.Rent(n);

        for (int i = 0; i < n; i++)
        {
            sa[i] = i;
            rank[i] = s[i];
        }

        for (int k = 1; k < n; k *= 2)
        {
            int kk = k;
            int[] r = rank;

            Comparison<int> cmp = (a, b) =>
            {
                if (r[a] != r[b]) return r[a] - r[b];
                int ra = r[(a + kk) % n];
                int rb = r[(b + kk) % n];
                return ra - rb;
            };

            Array.Sort(sa, 0, n, Comparer<int>.Create(cmp));

            tmp[sa[0]] = 0;
            for (int i = 1; i < n; i++)
            {
                bool diff = rank[sa[i]] != rank[sa[i - 1]] ||
                            rank[(sa[i] + kk) % n] != rank[(sa[i - 1] + kk) % n];
                tmp[sa[i]] = tmp[sa[i - 1]] + (diff ? 1 : 0);
            }

            Array.Copy(tmp, rank, n);

            if (rank[sa[n - 1]] == n - 1) break;
        }

        ArrayPool<int>.Shared.Return(rank);
        ArrayPool<int>.Shared.Return(tmp);

        return sa; // это арендованный массив — вызывающий код должен его вернуть
    }
}