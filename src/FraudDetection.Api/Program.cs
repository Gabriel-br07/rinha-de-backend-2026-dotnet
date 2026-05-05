using FraudDetection.Api.Dtos;
using FraudDetection.Api.Data;
using FraudDetection.Api.Options;
using FraudDetection.Api.Search;
using FraudDetection.Api.Serialization;
using FraudDetection.Api.Vectorization;
using Microsoft.Extensions.Options;
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

app.MapGet("/ready", (IndexStore store) =>
    store.IsReady ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapPost("/fraud-score", (FraudScoreRequest request, IndexStore store, TransactionVectorizer vectorizer, ExactKnnSearcher searcher) =>
{
    try
    {
        var index = store.TryGet();
        if (index is null)
            return Results.Json(new FraudScoreResponse(approved: true, fraud_score: 0.0f), AppJsonSerializerContext.Default.FraudScoreResponse);

        Span<float> v14 = stackalloc float[14];
        Span<byte> q14 = stackalloc byte[14];

        vectorizer.VectorizeTo14(request, v14);
        Quantizer.Encode14(v14, q14);

        var fraudScore = searcher.FraudScore5(index, q14);
        var approved = fraudScore < 0.6f;

        return Results.Json(new FraudScoreResponse(approved, fraudScore), AppJsonSerializerContext.Default.FraudScoreResponse);
    }
    catch
    {
        return Results.Json(new FraudScoreResponse(approved: true, fraud_score: 0.0f), AppJsonSerializerContext.Default.FraudScoreResponse);
    }
});

app.Run();
