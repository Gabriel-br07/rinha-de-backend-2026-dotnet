using System.Text.Json;
using FraudDetection.Api.Serialization;

namespace FraudDetection.Api.Vectorization;

public sealed class MccRiskProvider
{
    private readonly Dictionary<string, float> _map;

    private MccRiskProvider(Dictionary<string, float> map)
    {
        _map = map;
    }

    public static MccRiskProvider LoadFromFile(string path)
    {
        // Small map; JSON parse once at startup is fine.
        using var fs = File.OpenRead(path);
        var map = JsonSerializer.Deserialize(fs, AppJsonSerializerContext.Default.DictionaryStringSingle)
                  ?? new Dictionary<string, float>(StringComparer.Ordinal);

        return new MccRiskProvider(map);
    }

    public float GetRiskOrDefault(string? mcc)
    {
        if (mcc is null) return 0.5f;

        if (_map.TryGetValue(mcc, out var v))
            return v;

        return 0.5f;
    }
}

