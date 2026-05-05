namespace FraudDetection.Api.Options;

public sealed class DataPathsOptions
{
    public string ReferencesBinPath { get; init; } = "data/references.bin";
    public string LabelsBinPath { get; init; } = "data/labels.bin";
    public string NormalizationJsonPath { get; init; } = "resources/normalization.json";
    public string MccRiskJsonPath { get; init; } = "resources/mcc_risk.json";
}

