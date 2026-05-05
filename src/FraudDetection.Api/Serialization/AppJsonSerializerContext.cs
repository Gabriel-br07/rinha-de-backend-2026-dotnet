using System.Text.Json.Serialization;
using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;

namespace FraudDetection.Api.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FraudScoreRequest))]
[JsonSerializable(typeof(FraudScoreResponse))]
[JsonSerializable(typeof(Dictionary<string, float>))]
[JsonSerializable(typeof(NormalizationOptions))]
public partial class AppJsonSerializerContext : JsonSerializerContext;

