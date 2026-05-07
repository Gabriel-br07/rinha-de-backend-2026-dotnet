namespace FraudDetection.Api.Options;

public sealed class VectorSearchOptions
{
    // exact | ivf
    public string Mode { get; init; } = "exact";

    public int NProbe { get; init; } = 16;

    // If Mode == "ivf" but IVF files cannot be loaded, allow falling back to exact instead of failing readiness.
    public bool FallbackToExactOnIvfLoadFailure { get; init; } = false;
}
