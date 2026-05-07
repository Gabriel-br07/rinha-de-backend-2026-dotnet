using System.Collections.Frozen;
using System.Text.Json;
using FraudDetection.Api.Serialization;

namespace FraudDetection.Api.Vectorization;

public sealed class MccRiskProvider
{
    private readonly FrozenDictionary<string, float> _map;

    private MccRiskProvider(FrozenDictionary<string, float> map)
    {
        _map = map;
    }

    public static MccRiskProvider LoadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var raw = JsonSerializer.Deserialize(fs, AppJsonSerializerContext.Default.DictionaryStringSingle)
                  ?? new Dictionary<string, float>(StringComparer.Ordinal);

        var map = raw.ToFrozenDictionary(StringComparer.Ordinal);
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

