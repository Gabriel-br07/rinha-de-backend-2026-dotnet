using FraudDetection.Api.Options;
using Microsoft.Extensions.Logging;

namespace FraudDetection.Api.Data;

public sealed class IndexStore
{
    private readonly ILogger<IndexStore> _logger;
    private volatile VectorIndex? _index;
    private volatile Exception? _error;

    public bool IsReady => _index is not null;
    public Exception? Error => _error;

    public VectorIndex? TryGet() => _index;

    public IndexStore(ILogger<IndexStore> logger)
    {
        _logger = logger;
    }

    public void Load(DataPathsOptions paths)
    {
        _logger.LogInformation(
            "Vector index load started (references={References}, labels={Labels})",
            paths.ReferencesBinPath,
            paths.LabelsBinPath);

        try
        {
            _index = VectorIndexLoader.Load(paths);
            _logger.LogInformation(
                "Vector index load completed. ReferenceCount={ReferenceCount}",
                _index.ReferenceCount);
        }
        catch (Exception ex)
        {
            _error = ex;
            _logger.LogError(ex, "Vector index load failed");
        }
    }
}
