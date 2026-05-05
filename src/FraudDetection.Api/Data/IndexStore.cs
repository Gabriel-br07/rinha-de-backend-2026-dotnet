using FraudDetection.Api.Options;

namespace FraudDetection.Api.Data;

public sealed class IndexStore
{
    private volatile VectorIndex? _index;
    private volatile Exception? _error;

    public bool IsReady => _index is not null;
    public Exception? Error => _error;

    public VectorIndex? TryGet() => _index;

    public void Load(DataPathsOptions paths)
    {
        try
        {
            _index = VectorIndexLoader.Load(paths);
        }
        catch (Exception ex)
        {
            _error = ex;
        }
    }
}

