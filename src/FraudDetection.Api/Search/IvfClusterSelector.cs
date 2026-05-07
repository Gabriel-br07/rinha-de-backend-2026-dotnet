using System.Runtime.CompilerServices;
using FraudDetection.Api.Data;

namespace FraudDetection.Api.Search;

public sealed class IvfClusterSelector
{
    public const int MaxNProbe = 128;

    public int SelectTopNProbe(IvfIndex index, ReadOnlySpan<byte> query14, int nprobe, Span<int> bestClusterIds)
    {
        if (query14.Length != IvfIndex.Dim)
            throw new ArgumentException($"query must be {IvfIndex.Dim} bytes", nameof(query14));

        if (nprobe <= 0)
            return 0;

        if (nprobe > MaxNProbe)
            nprobe = MaxNProbe;

        if (bestClusterIds.Length < nprobe)
            throw new ArgumentException($"bestClusterIds must have length >= {nprobe}", nameof(bestClusterIds));

        Span<int> bestDist = stackalloc int[MaxNProbe];
        for (var i = 0; i < nprobe; i++)
        {
            bestDist[i] = int.MaxValue;
            bestClusterIds[i] = -1;
        }

        var centroids = index.Centroids;
        var nlist = index.NList;

        var centOffset = 0;
        for (var c = 0; c < nlist; c++, centOffset += IvfIndex.Dim)
        {
            var d = Distance14(query14, centroids, centOffset);
            if (d >= bestDist[nprobe - 1])
                continue;

            // Insert into sorted bestDist/bestClusterIds (ascending by distance)
            var pos = nprobe - 1;
            while (pos > 0 && d < bestDist[pos - 1])
                pos--;

            for (var i = nprobe - 1; i > pos; i--)
            {
                bestDist[i] = bestDist[i - 1];
                bestClusterIds[i] = bestClusterIds[i - 1];
            }

            bestDist[pos] = d;
            bestClusterIds[pos] = c;
        }

        // If nprobe > nlist, trailing entries may remain -1; return actual count.
        var actual = 0;
        for (var i = 0; i < nprobe; i++)
        {
            if (bestClusterIds[i] >= 0) actual++;
            else break;
        }

        return actual;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Distance14(ReadOnlySpan<byte> a14, byte[] b, int bOff)
    {
        var d0 = (int)a14[0] - b[bOff + 0];
        var d1 = (int)a14[1] - b[bOff + 1];
        var d2 = (int)a14[2] - b[bOff + 2];
        var d3 = (int)a14[3] - b[bOff + 3];
        var d4 = (int)a14[4] - b[bOff + 4];
        var d5 = (int)a14[5] - b[bOff + 5];
        var d6 = (int)a14[6] - b[bOff + 6];
        var d7 = (int)a14[7] - b[bOff + 7];
        var d8 = (int)a14[8] - b[bOff + 8];
        var d9 = (int)a14[9] - b[bOff + 9];
        var d10 = (int)a14[10] - b[bOff + 10];
        var d11 = (int)a14[11] - b[bOff + 11];
        var d12 = (int)a14[12] - b[bOff + 12];
        var d13 = (int)a14[13] - b[bOff + 13];

        return
            d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 +
            d4 * d4 + d5 * d5 + d6 * d6 + d7 * d7 +
            d8 * d8 + d9 * d9 + d10 * d10 + d11 * d11 +
            d12 * d12 + d13 * d13;
    }
}

