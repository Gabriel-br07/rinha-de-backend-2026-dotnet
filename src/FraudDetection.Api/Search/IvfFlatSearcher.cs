using System.Runtime.CompilerServices;
using FraudDetection.Api.Data;

namespace FraudDetection.Api.Search;

public sealed class IvfFlatSearcher
{
    private readonly IvfClusterSelector _selector = new();

    public float FraudScore5(IvfIndex index, ReadOnlySpan<byte> query14, int nprobe)
    {
        Span<int> bestDist = stackalloc int[5];
        Span<byte> bestLabel = stackalloc byte[5];

        bestDist[0] = int.MaxValue;
        bestDist[1] = int.MaxValue;
        bestDist[2] = int.MaxValue;
        bestDist[3] = int.MaxValue;
        bestDist[4] = int.MaxValue;

        var probe = nprobe;
        if (probe <= 0) probe = 1;
        if (probe > IvfClusterSelector.MaxNProbe) probe = IvfClusterSelector.MaxNProbe;
        if (probe > index.NList) probe = index.NList;

        Span<int> bestClusterIds = stackalloc int[IvfClusterSelector.MaxNProbe];
        var selected = _selector.SelectTopNProbe(index, query14, probe, bestClusterIds);

        var vectors = index.Vectors;
        var labels = index.Labels;
        var offsets = index.Offsets;

        for (var i = 0; i < selected; i++)
        {
            var c = bestClusterIds[i];
            var start = offsets[c];
            var end = offsets[c + 1];
            ScanRangeTop5(vectors, labels, start, end, query14, bestDist, bestLabel);
        }

        var neighborCount = 0;
        var fraud = 0;
        for (var i = 0; i < 5; i++)
        {
            if (bestDist[i] == int.MaxValue)
                break;
            neighborCount++;
            if (bestLabel[i] == 1)
                fraud++;
        }

        if (neighborCount == 0)
            return 1.0f;

        return fraud / (float)neighborCount;
    }

    private static void ScanRangeTop5(
        byte[] vectors,
        byte[] labels,
        int start,
        int end,
        ReadOnlySpan<byte> query14,
        Span<int> bestDist,
        Span<byte> bestLabel)
    {
        var vecOffset = start * IvfIndex.Dim;
        for (var idx = start; idx < end; idx++, vecOffset += IvfIndex.Dim)
        {
            var dist = Distance14(query14, vectors, vecOffset);
            if (dist >= bestDist[4])
                continue;

            InsertTop5(bestDist, bestLabel, dist, labels[idx]);
        }
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

    private static void InsertTop5(Span<int> dist, Span<byte> label, int newDist, byte newLabel)
    {
        var pos = 4;
        if (newDist < dist[0]) pos = 0;
        else if (newDist < dist[1]) pos = 1;
        else if (newDist < dist[2]) pos = 2;
        else if (newDist < dist[3]) pos = 3;
        else pos = 4;

        for (var i = 4; i > pos; i--)
        {
            dist[i] = dist[i - 1];
            label[i] = label[i - 1];
        }

        dist[pos] = newDist;
        label[pos] = newLabel;
    }
}
