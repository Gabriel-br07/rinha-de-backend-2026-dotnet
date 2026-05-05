namespace FraudDetection.Api.Options;

public sealed class NormalizationOptions
{
    public float max_amount { get; init; } = 10000f;
    public float max_installments { get; init; } = 12f;
    public float amount_vs_avg_ratio { get; init; } = 10f;
    public float max_minutes { get; init; } = 1440f;
    public float max_km { get; init; } = 1000f;
    public float max_tx_count_24h { get; init; } = 20f;
    public float max_merchant_avg_amount { get; init; } = 10000f;
}

