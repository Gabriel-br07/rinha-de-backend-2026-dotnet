using FraudDetection.Api.Data;

namespace FraudDetection.Api.Search;

public sealed class ExactKnnSearcher
{
    public float FraudScore5(VectorIndex index, ReadOnlySpan<byte> query14)
    {
        // Keep top-5 smallest distances.
        Span<int> bestDist = stackalloc int[5];
        Span<byte> bestLabel = stackalloc byte[5];

        bestDist[0] = int.MaxValue;
        bestDist[1] = int.MaxValue;
        bestDist[2] = int.MaxValue;
        bestDist[3] = int.MaxValue;
        bestDist[4] = int.MaxValue;

        var vectors = index.Vectors;
        var labels = index.Labels;

        var count = labels.Length;
        var vecOffset = 0;

        for (var idx = 0; idx < count; idx++, vecOffset += 14)
        {
            int dist = 0;

            for (var d = 0; d < 14; d++)
            {
                var diff = (int)query14[d] - vectors[vecOffset + d];
                dist += diff * diff;
            }

            if (dist >= bestDist[4])
                continue;

            InsertTop5(bestDist, bestLabel, dist, labels[idx]);
        }

        var fraud = 0;
        if (bestLabel[0] == 1) fraud++;
        if (bestLabel[1] == 1) fraud++;
        if (bestLabel[2] == 1) fraud++;
        if (bestLabel[3] == 1) fraud++;
        if (bestLabel[4] == 1) fraud++;

        return fraud / 5.0f;
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

