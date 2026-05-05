namespace FraudDetection.Api.Vectorization;

internal static class Clamp
{
    public static float Clamp01(float v)
    {
        if (v <= 0f) return 0f;
        if (v >= 1f) return 1f;
        return v;
    }
}

