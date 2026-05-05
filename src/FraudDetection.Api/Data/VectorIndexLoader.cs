using FraudDetection.Api.Options;

namespace FraudDetection.Api.Data;

public static class VectorIndexLoader
{
    public static VectorIndex Load(DataPathsOptions paths)
    {
        var vectors = File.ReadAllBytes(paths.ReferencesBinPath);
        var labels = File.ReadAllBytes(paths.LabelsBinPath);

        if (vectors.Length % 14 != 0)
            throw new InvalidDataException($"'{paths.ReferencesBinPath}' size {vectors.Length} is not divisible by 14");

        var count = vectors.Length / 14;
        if (labels.Length != count)
            throw new InvalidDataException($"'{paths.LabelsBinPath}' size {labels.Length} doesn't match vector count {count}");

        return new VectorIndex(vectors, labels);
    }
}

