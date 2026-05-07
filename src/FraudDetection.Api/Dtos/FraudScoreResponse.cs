namespace FraudDetection.Api.Dtos;

public readonly record struct FraudScoreResponse(bool approved, float fraud_score);

