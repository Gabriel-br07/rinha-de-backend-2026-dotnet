using FraudDetection.Api.Options;

namespace FraudDetection.Api.Data;

public static class IvfIndexLoader
{
    public static IvfIndex Load(DataPathsOptions paths)
    {
        // Read files
        var centroidBytes = File.ReadAllBytes(paths.IvfCentroidsBinPath);
        var offsetsBytes = File.ReadAllBytes(paths.IvfOffsetsBinPath);
        var vectors = File.ReadAllBytes(paths.IvfVectorsBinPath);
        var labels = File.ReadAllBytes(paths.IvfLabelsBinPath);

        // Parse centroids header: int32 nlist, int32 dim, then byte[nlist*dim]
        if (centroidBytes.Length < 8)
            throw new InvalidDataException($"'{paths.IvfCentroidsBinPath}' is too small ({centroidBytes.Length} bytes)");

        var nlist = BitConverter.ToInt32(centroidBytes, 0);
        var dim = BitConverter.ToInt32(centroidBytes, 4);

        if (nlist <= 0)
            throw new InvalidDataException($"'{paths.IvfCentroidsBinPath}' invalid nlist={nlist}");

        if (dim != IvfIndex.Dim)
            throw new InvalidDataException($"'{paths.IvfCentroidsBinPath}' invalid dim={dim}, expected {IvfIndex.Dim}");

        var expectedCentroidsLen = checked(8 + (nlist * IvfIndex.Dim));
        if (centroidBytes.Length != expectedCentroidsLen)
            throw new InvalidDataException($"'{paths.IvfCentroidsBinPath}' size {centroidBytes.Length} doesn't match header nlist={nlist}, dim={IvfIndex.Dim} (expected {expectedCentroidsLen})");

        var centroids = new byte[nlist * IvfIndex.Dim];
        Buffer.BlockCopy(centroidBytes, 8, centroids, 0, centroids.Length);

        // Parse offsets: int32 nlist, then int32[nlist+1]
        var expectedOffsetsLen = checked(4 + ((nlist + 1) * 4));
        if (offsetsBytes.Length != expectedOffsetsLen)
            throw new InvalidDataException($"'{paths.IvfOffsetsBinPath}' size {offsetsBytes.Length} doesn't match nlist={nlist} (expected {expectedOffsetsLen})");

        var offsetsNlist = BitConverter.ToInt32(offsetsBytes, 0);
        if (offsetsNlist != nlist)
            throw new InvalidDataException($"'{paths.IvfOffsetsBinPath}' header nlist={offsetsNlist} doesn't match centroids nlist={nlist}");

        var offsets = new int[nlist + 1];
        Buffer.BlockCopy(offsetsBytes, 4, offsets, 0, (nlist + 1) * 4);

        ValidateOffsets(paths.IvfOffsetsBinPath, offsets);

        var count = offsets[nlist];
        if (count < 0)
            throw new InvalidDataException($"'{paths.IvfOffsetsBinPath}' invalid count={count}");

        if (vectors.Length != checked(count * IvfIndex.Dim))
            throw new InvalidDataException($"'{paths.IvfVectorsBinPath}' size {vectors.Length} doesn't match count={count} (expected {count * IvfIndex.Dim})");

        if (labels.Length != count)
            throw new InvalidDataException($"'{paths.IvfLabelsBinPath}' size {labels.Length} doesn't match count={count}");

        return new IvfIndex
        {
            NList = nlist,
            Count = count,
            Centroids = centroids,
            Offsets = offsets,
            Vectors = vectors,
            Labels = labels,
        };
    }

    private static void ValidateOffsets(string offsetsPath, int[] offsets)
    {
        if (offsets.Length < 2)
            throw new InvalidDataException($"'{offsetsPath}' offsets length {offsets.Length} is invalid");

        if (offsets[0] != 0)
            throw new InvalidDataException($"'{offsetsPath}' offsets[0] must be 0 (got {offsets[0]})");

        var prev = offsets[0];
        for (var i = 1; i < offsets.Length; i++)
        {
            var v = offsets[i];
            if (v < prev)
                throw new InvalidDataException($"'{offsetsPath}' offsets not monotonic at i={i}: {prev} -> {v}");
            prev = v;
        }
    }
}

