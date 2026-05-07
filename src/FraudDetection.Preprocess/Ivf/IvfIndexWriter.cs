using System.Buffers.Binary;

namespace FraudDetection.Preprocess.Ivf;

internal static class IvfIndexWriter
{
    public static void WriteAll(
        string outDir,
        int nlist,
        byte[] centroids,
        int[] offsets,
        byte[] groupedVectors,
        byte[] groupedLabels)
    {
        Directory.CreateDirectory(outDir);

        WriteCentroids(Path.Combine(outDir, "ivf_centroids.bin"), nlist, centroids);
        WriteOffsets(Path.Combine(outDir, "ivf_offsets.bin"), nlist, offsets);
        File.WriteAllBytes(Path.Combine(outDir, "ivf_vectors.bin"), groupedVectors);
        File.WriteAllBytes(Path.Combine(outDir, "ivf_labels.bin"), groupedLabels);
    }

    private static void WriteCentroids(string path, int nlist, byte[] centroids)
    {
        if (centroids.Length != checked(nlist * 14))
            throw new InvalidOperationException($"centroids length {centroids.Length} doesn't match nlist={nlist}");

        var buf = new byte[8 + centroids.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), nlist);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), 14);
        Buffer.BlockCopy(centroids, 0, buf, 8, centroids.Length);
        File.WriteAllBytes(path, buf);
    }

    private static void WriteOffsets(string path, int nlist, int[] offsets)
    {
        if (offsets.Length != nlist + 1)
            throw new InvalidOperationException($"offsets length {offsets.Length} doesn't match nlist+1={nlist + 1}");

        var buf = new byte[4 + (offsets.Length * 4)];
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), nlist);
        Buffer.BlockCopy(offsets, 0, buf, 4, offsets.Length * 4);
        File.WriteAllBytes(path, buf);
    }
}

