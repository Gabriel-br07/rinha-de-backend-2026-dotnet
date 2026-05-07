using FraudDetection.Api.Options;
using Microsoft.Extensions.Logging;

namespace FraudDetection.Api.Data;

public sealed class IndexStore
{
    private readonly ILogger<IndexStore> _logger;
    private volatile VectorIndex? _exact;
    private volatile IvfIndex? _ivf;
    private volatile Exception? _error;
    private volatile string _activeMode = "none";

    public bool IsReady => _activeMode != "none";
    public Exception? Error => _error;

    public string ActiveMode => _activeMode;

    public VectorIndex? TryGetExact() => _exact;
    public IvfIndex? TryGetIvf() => _ivf;

    public IndexStore(ILogger<IndexStore> logger)
    {
        _logger = logger;
    }

    public void Load(DataPathsOptions paths, VectorSearchOptions search)
    {
        try
        {
            var mode = (search.Mode ?? "exact").Trim().ToLowerInvariant();
            if (mode == "ivf")
            {
                LoadIvf(paths, search);
                return;
            }

            LoadExact(paths);
        }
        catch (Exception ex)
        {
            _error = ex;
            _logger.LogError(ex, "Vector index load failed");
        }
    }

    private void LoadExact(DataPathsOptions paths)
    {
        _logger.LogInformation(
            "Exact index load started (references={References}, labels={Labels})",
            paths.ReferencesBinPath,
            paths.LabelsBinPath);

        _exact = VectorIndexLoader.Load(paths);
        _activeMode = "exact";

        _logger.LogInformation(
            "Exact index load completed. ReferenceCount={ReferenceCount}",
            _exact.ReferenceCount);
    }

    private void LoadIvf(DataPathsOptions paths, VectorSearchOptions search)
    {
        _logger.LogInformation(
            "IVF index load started (centroids={Centroids}, offsets={Offsets}, vectors={Vectors}, labels={Labels})",
            paths.IvfCentroidsBinPath,
            paths.IvfOffsetsBinPath,
            paths.IvfVectorsBinPath,
            paths.IvfLabelsBinPath);

        try
        {
            _ivf = IvfIndexLoader.Load(paths);
            _activeMode = "ivf";

            var nlist = _ivf.NList;
            var offsets = _ivf.Offsets;
            var empty = 0;
            for (var c = 0; c < nlist; c++)
            {
                if (offsets[c + 1] - offsets[c] == 0) empty++;
            }

            _logger.LogInformation(
                "IVF index load completed. NList={NList} Count={Count} EmptyClusters={EmptyClusters} NProbe={NProbe}",
                _ivf.NList,
                _ivf.Count,
                empty,
                search.NProbe);
        }
        catch (Exception ex)
        {
            _error = ex;
            _logger.LogError(ex, "IVF index load failed");

            if (search.FallbackToExactOnIvfLoadFailure)
            {
                _logger.LogWarning("IVF load failed; falling back to exact because FallbackToExactOnIvfLoadFailure=true");
                LoadExact(paths);
                return;
            }

            _activeMode = "none";
        }
    }
}
