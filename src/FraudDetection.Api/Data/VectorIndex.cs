namespace FraudDetection.Api.Data;

public sealed class VectorIndex
{
    public readonly byte[] Vectors;
    public readonly byte[] Labels;

    public int ReferenceCount => Labels.Length;

    public VectorIndex(byte[] vectors, byte[] labels)
    {
        Vectors = vectors;
        Labels = labels;
    }
}

