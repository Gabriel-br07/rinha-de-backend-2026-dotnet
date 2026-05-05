using FraudDetection.Api.Dtos;
using FraudDetection.Api.Data;
using FraudDetection.Api.Options;
using FraudDetection.Api.Search;
using FraudDetection.Api.Serialization;
using FraudDetection.Api.Vectorization;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(static o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddOptions<DataPathsOptions>()
    .Bind(builder.Configuration.GetSection("Paths"));
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

var app = builder.Build();

app.Logger.LogInformation("FraudDetection API application starting (HTTP pipeline configured)");

app.MapGet("/ready", (IndexStore store) =>
    store.IsReady ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapPost("/fraud-score", (FraudScoreRequest request, IndexStore store, TransactionVectorizer vectorizer, ExactKnnSearcher searcher, ILogger<Program> logger) =>
{
    // Obs: timings com Stopwatch são temporários (diagnóstico de gargalos); remover ou condicionar depois para menos overhead no hot path.
    var swTotal = Stopwatch.StartNew();
    try
    {
        var index = store.TryGet();
        if (index is null)
        {
            if (Random.Shared.Next(128) == 0)
            {
                logger.LogInformation(
                    "FraudScore sample: index not loaded, totalMs={TotalMs:F3}",
                    swTotal.Elapsed.TotalMilliseconds);
            }

            return Results.Json(new FraudScoreResponse(approved: true, fraud_score: 0.0f), AppJsonSerializerContext.Default.FraudScoreResponse);
        }

        Span<float> v14 = stackalloc float[14];
        Span<byte> q14 = stackalloc byte[14];

        var swVector = Stopwatch.StartNew();
        vectorizer.VectorizeTo14(request, v14);
        Quantizer.Encode14(v14, q14);
        var vectorMs = swVector.Elapsed.TotalMilliseconds;

        var swSearch = Stopwatch.StartNew();
        var fraudScore = searcher.FraudScore5(index, q14);
        var searchMs = swSearch.Elapsed.TotalMilliseconds;

        var approved = fraudScore < 0.6f;

        if (Random.Shared.Next(128) == 0)
        {
            logger.LogInformation(
                "FraudScore sample: totalMs={TotalMs:F3} vectorizeMs={VectorMs:F3} searchMs={SearchMs:F3}",
                swTotal.Elapsed.TotalMilliseconds,
                vectorMs,
                searchMs);
        }

        return Results.Json(new FraudScoreResponse(approved, fraudScore), AppJsonSerializerContext.Default.FraudScoreResponse);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Fraud-score unexpected exception");
        return Results.Json(new FraudScoreResponse(approved: true, fraud_score: 0.0f), AppJsonSerializerContext.Default.FraudScoreResponse);
    }
});

app.Run();
