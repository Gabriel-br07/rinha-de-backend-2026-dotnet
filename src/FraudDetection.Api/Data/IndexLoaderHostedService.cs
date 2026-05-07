using FraudDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace FraudDetection.Api.Data;

public sealed class IndexLoaderHostedService : IHostedService
{
    private readonly IndexStore _store;
    private readonly DataPathsOptions _paths;
    private readonly VectorSearchOptions _search;

    public IndexLoaderHostedService(IndexStore store, IOptions<DataPathsOptions> paths, IOptions<VectorSearchOptions> search)
    {
        _store = store;
        _paths = paths.Value;
        _search = search.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Run once in background so the host can start and /ready can return 503 until loaded.
        _ = Task.Run(() => _store.Load(_paths, _search), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

