namespace FraudDetection.Api.Data;

public sealed class IvfIndex
{
    public const int Dim = 14;

    // Layout: nlist * 14
    public required byte[] Centroids { get; init; }

    // Length: nlist + 1
    public required int[] Offsets { get; init; }

    // Layout: count * 14, grouped by cluster
    public required byte[] Vectors { get; init; }

    // Length: count, same order as grouped vectors
    public required byte[] Labels { get; init; }

    public int NList { get; init; }

    public int Count { get; init; }
}

