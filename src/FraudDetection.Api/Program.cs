using FraudDetection.Api.Data;
using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;
using FraudDetection.Api.Search;
using FraudDetection.Api.Serialization;
using FraudDetection.Api.Vectorization;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

ApplyEnvOverride(builder, "VECTOR_SEARCH_MODE", "VectorSearch:Mode");
ApplyEnvOverride(builder, "IVF_NPROBE", "VectorSearch:NProbe");

builder.Services.ConfigureHttpJsonOptions(static o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddOptions<DataPathsOptions>()
    .Bind(builder.Configuration.GetSection("Paths"));
builder.Services.AddOptions<VectorSearchOptions>()
    .Bind(builder.Configuration.GetSection("VectorSearch"));

builder.Services.AddSingleton<IndexStore>();
builder.Services.AddHostedService<IndexLoaderHostedService>();

builder.Services.AddSingleton(sp =>
{
    var paths = sp.GetRequiredService<IOptions<DataPathsOptions>>().Value;
    using var fs = File.OpenRead(paths.NormalizationJsonPath);
    return JsonSerializer.Deserialize(fs, AppJsonSerializerContext.Default.NormalizationOptions) ?? new NormalizationOptions();
});

builder.Services.AddSingleton(sp =>
{
    var paths = sp.GetRequiredService<IOptions<DataPathsOptions>>().Value;
    return MccRiskProvider.LoadFromFile(paths.MccRiskJsonPath);
});

builder.Services.AddSingleton<TransactionVectorizer>();
builder.Services.AddSingleton<ExactKnnSearcher>();
builder.Services.AddSingleton<IvfFlatSearcher>();

var app = builder.Build();

app.Logger.LogInformation("FraudDetection API application starting (HTTP pipeline configured)");

// Resolve dependencies once at startup so the handler avoids per-request DI / IOptions lookups.
var store = app.Services.GetRequiredService<IndexStore>();
var vectorizer = app.Services.GetRequiredService<TransactionVectorizer>();
var exactSearcher = app.Services.GetRequiredService<ExactKnnSearcher>();
var ivfSearcher = app.Services.GetRequiredService<IvfFlatSearcher>();
var searchOptions = app.Services.GetRequiredService<IOptions<VectorSearchOptions>>().Value;
var hotPathLogger = app.Logger;

var ivfMode = string.Equals(
    (searchOptions.Mode ?? "exact").Trim(),
    "ivf",
    StringComparison.OrdinalIgnoreCase);
var nprobe = searchOptions.NProbe;

var fallbackResponse = new FraudScoreResponse(approved: true, fraud_score: 0.0f);

app.Logger.LogInformation("Hot-path config: ivfMode={IvfMode} nprobe={NProbe}", ivfMode, nprobe);

app.MapGet("/ready", (IndexStore s) =>
    s.IsReady ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapPost("/fraud-score", (FraudScoreRequest request) =>
{
    try
    {
        IvfIndex? indexIvf = null;
        VectorIndex? indexExact = null;

        if (ivfMode)
        {
            indexIvf = store.TryGetIvf();
            if (indexIvf is null)
            {
                return Results.Json(fallbackResponse, AppJsonSerializerContext.Default.FraudScoreResponse);
            }
        }
        else
        {
            indexExact = store.TryGetExact();
            if (indexExact is null)
            {
                return Results.Json(fallbackResponse, AppJsonSerializerContext.Default.FraudScoreResponse);
            }
        }

        Span<float> v14 = stackalloc float[14];
        Span<byte> q14 = stackalloc byte[14];

        vectorizer.VectorizeTo14(request, v14);
        Quantizer.Encode14(v14, q14);

        var fraudScore = ivfMode
            ? ivfSearcher.FraudScore5(indexIvf!, q14, nprobe)
            : exactSearcher.FraudScore5(indexExact!, q14);

        var approved = fraudScore < 0.6f;
        return Results.Json(
            new FraudScoreResponse(approved, fraudScore),
            AppJsonSerializerContext.Default.FraudScoreResponse);
    }
    catch (Exception ex)
    {
        hotPathLogger.LogWarning(ex, "Fraud-score unexpected exception");
        return Results.Json(fallbackResponse, AppJsonSerializerContext.Default.FraudScoreResponse);
    }
});

app.Run();

static void ApplyEnvOverride(WebApplicationBuilder b, string envName, string configKey)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        b.Configuration[configKey] = value;
    }
}
