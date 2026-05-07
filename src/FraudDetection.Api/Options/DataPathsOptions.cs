namespace FraudDetection.Api.Options;

public sealed class DataPathsOptions
{
    public string ReferencesBinPath { get; init; } = "data/references.bin";
    public string LabelsBinPath { get; init; } = "data/labels.bin";
    public string IvfCentroidsBinPath { get; init; } = "data/ivf_centroids.bin";
    public string IvfOffsetsBinPath { get; init; } = "data/ivf_offsets.bin";
    public string IvfVectorsBinPath { get; init; } = "data/ivf_vectors.bin";
    public string IvfLabelsBinPath { get; init; } = "data/ivf_labels.bin";
    public string NormalizationJsonPath { get; init; } = "resources/normalization.json";
    public string MccRiskJsonPath { get; init; } = "resources/mcc_risk.json";
}

