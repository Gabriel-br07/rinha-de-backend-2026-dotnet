namespace FraudDetection.Api.Vectorization;

public static class Quantizer
{
    public static byte Encode(float v)
    {
        if (v == -1f) return 0;
        if (v <= 0f) return 1;
        if (v >= 1f) return 255;

        var scaled = (int)MathF.Round(v * 254f);
        if (scaled <= 0) return 1;
        if (scaled >= 254) return 255;
        return (byte)(1 + scaled);
    }

    public static void Encode14(ReadOnlySpan<float> v14, Span<byte> dst14)
    {
        for (var i = 0; i < 14; i++)
            dst14[i] = Encode(v14[i]);
    }
}

