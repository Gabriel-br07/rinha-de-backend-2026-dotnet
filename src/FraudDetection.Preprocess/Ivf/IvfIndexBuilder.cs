namespace FraudDetection.Preprocess.Ivf;

internal static class IvfIndexBuilder
{
    public const int Dim = 14;

    public static byte[] PickRandomCentroids(byte[] vectors, int count, int nlist, int seed = 42)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (nlist <= 0) throw new ArgumentOutOfRangeException(nameof(nlist));
        if (vectors.Length != checked(count * Dim))
            throw new ArgumentException("vectors length mismatch", nameof(vectors));

        var rng = new Random(seed);
        var centroids = new byte[nlist * Dim];

        for (var c = 0; c < nlist; c++)
        {
            var idx = rng.Next(count);
            Buffer.BlockCopy(vectors, idx * Dim, centroids, c * Dim, Dim);
        }

        return centroids;
    }

    public static void Build(
        byte[] vectors,
        byte[] labels,
        int nlist,
        out byte[] centroids,
        out int[] offsets,
        out byte[] groupedVectors,
        out byte[] groupedLabels,
        int seed = 42)
    {
        if (vectors.Length % Dim != 0)
            throw new InvalidDataException($"vectors length {vectors.Length} is not divisible by {Dim}");

        var count = vectors.Length / Dim;
        if (labels.Length != count)
            throw new InvalidDataException($"labels length {labels.Length} doesn't match vector count {count}");

        if (nlist <= 0)
            throw new ArgumentOutOfRangeException(nameof(nlist));

        centroids = PickRandomCentroids(vectors, count, nlist, seed);

        // First pass: assign each vector once and count per cluster
        var assignments = new int[count];
        var clusterCounts = new int[nlist];
        for (var i = 0; i < count; i++)
        {
            var cluster = FindNearestCentroid(vectors, i * Dim, centroids, nlist);
            assignments[i] = cluster;
            clusterCounts[cluster]++;
        }

        // Build offsets
        offsets = new int[nlist + 1];
        var sum = 0;
        offsets[0] = 0;
        for (var c = 0; c < nlist; c++)
        {
            sum += clusterCounts[c];
            offsets[c + 1] = sum;
        }

        // Second pass: fill grouped arrays
        groupedVectors = new byte[vectors.Length];
        groupedLabels = new byte[labels.Length];

        // Current write cursor per cluster (starts at offsets[c])
        var cursor = new int[nlist];
        Array.Copy(offsets, 0, cursor, 0, nlist);

        for (var i = 0; i < count; i++)
        {
            var cluster = assignments[i];
            var pos = cursor[cluster]++;

            Buffer.BlockCopy(vectors, i * Dim, groupedVectors, pos * Dim, Dim);
            groupedLabels[pos] = labels[i];
        }

        // Basic sanity: all cursors should end at offsets[c+1]
        for (var c = 0; c < nlist; c++)
        {
            if (cursor[c] != offsets[c + 1])
                throw new InvalidOperationException($"cluster fill mismatch for c={c}: cursor={cursor[c]} expected={offsets[c + 1]}");
        }
    }

    private static int FindNearestCentroid(byte[] vectors, int vecOffset, byte[] centroids, int nlist)
    {
        var bestC = 0;
        var bestD = int.MaxValue;

        var centOffset = 0;
        for (var c = 0; c < nlist; c++, centOffset += Dim)
        {
            var d = Distance14(vectors, vecOffset, centroids, centOffset);
            if (d < bestD)
            {
                bestD = d;
                bestC = c;
            }
        }

        return bestC;
    }

    private static int Distance14(byte[] a, int aOff, byte[] b, int bOff)
    {
        var d0 = (int)a[aOff + 0] - b[bOff + 0];
        var d1 = (int)a[aOff + 1] - b[bOff + 1];
        var d2 = (int)a[aOff + 2] - b[bOff + 2];
        var d3 = (int)a[aOff + 3] - b[bOff + 3];
        var d4 = (int)a[aOff + 4] - b[bOff + 4];
        var d5 = (int)a[aOff + 5] - b[bOff + 5];
        var d6 = (int)a[aOff + 6] - b[bOff + 6];
        var d7 = (int)a[aOff + 7] - b[bOff + 7];
        var d8 = (int)a[aOff + 8] - b[bOff + 8];
        var d9 = (int)a[aOff + 9] - b[bOff + 9];
        var d10 = (int)a[aOff + 10] - b[bOff + 10];
        var d11 = (int)a[aOff + 11] - b[bOff + 11];
        var d12 = (int)a[aOff + 12] - b[bOff + 12];
        var d13 = (int)a[aOff + 13] - b[bOff + 13];

        return
            d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 +
            d4 * d4 + d5 * d5 + d6 * d6 + d7 * d7 +
            d8 * d8 + d9 * d9 + d10 * d10 + d11 * d11 +
            d12 * d12 + d13 * d13;
    }
}

