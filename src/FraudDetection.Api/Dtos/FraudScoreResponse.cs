namespace FraudDetection.Api.Dtos;

public sealed record FraudScoreResponse(bool approved, float fraud_score);

