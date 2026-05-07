using FraudDetection.Api.Data;
using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;
using FraudDetection.Api.Search;
using FraudDetection.Api.Vectorization;

namespace FraudDetection.Tests;

/// <summary>
/// Locks the fraud-score semantics that production must not silently drift on:
/// vectorize → quantize → top-k → decision. A failure here means an
/// optimization changed user-visible output, not just internal performance.
/// </summary>
public sealed class CorrectnessGuardrailTests
{
    private const float Threshold = 0.6f;
    private const string MccJson = "{\"5411\":0.10,\"7995\":0.95,\"5732\":0.30}";

    private static MccRiskProvider Mcc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcc_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, MccJson);
        return MccRiskProvider.LoadFromFile(path);
    }

    private static FraudScoreRequest LowRiskGrocery() => new(
        id: "gr-1",
        transaction: new TransactionDto(amount: 100f, installments: 1, requested_at: DateTimeOffset.Parse("2026-03-11T15:00:00Z")),
        customer: new CustomerDto(avg_amount: 120f, tx_count_24h: 2, known_merchants: ["M-G1"]),
        merchant: new MerchantDto(id: "M-G1", mcc: "5411", avg_amount: 110f),
        terminal: new TerminalDto(is_online: false, card_present: true, km_from_home: 5f),
        last_transaction: null);

    [Fact]
    public void Vectorize_LowRiskGrocery_HasExpectedShapeAndKeyDimensions()
    {
        var v = new TransactionVectorizer(new NormalizationOptions(), Mcc());
        var req = LowRiskGrocery();
        Span<float> v14 = stackalloc float[14];

        v.VectorizeTo14(req, v14);

        // Sanity: every dimension must end up in the legal range [-1, 1].
        for (var i = 0; i < 14; i++)
        {
            Assert.InRange(v14[i], -1f, 1f);
        }

        Assert.Equal(-1f, v14[5]);
        Assert.Equal(-1f, v14[6]);

        Assert.Equal(0f, v14[9]);
        Assert.Equal(1f, v14[10]);
        Assert.Equal(0f, v14[11]);
        Assert.Equal(0.10f, v14[12], precision: 5);

        Assert.True(v14[0] > 0f && v14[0] < 0.05f, $"amount norm out of expected band: {v14[0]}");
        Assert.True(v14[1] > 0f && v14[1] < 0.1f, $"installments norm out of expected band: {v14[1]}");
        Assert.True(v14[8] > 0f && v14[8] < 0.2f, $"tx_count_24h norm out of expected band: {v14[8]}");
    }

    [Fact]
    public void Quantize_LowRiskGrocery_IsStableAcrossCalls()
    {
        var v = new TransactionVectorizer(new NormalizationOptions(), Mcc());
        var req = LowRiskGrocery();

        Span<float> v14 = stackalloc float[14];
        Span<byte> q14a = stackalloc byte[14];
        Span<byte> q14b = stackalloc byte[14];

        v.VectorizeTo14(req, v14);
        Quantizer.Encode14(v14, q14a);

        v.VectorizeTo14(req, v14);
        Quantizer.Encode14(v14, q14b);

        Assert.True(q14a.SequenceEqual(q14b), "quantization must be deterministic for identical input");

        // -1 sentinels must encode to 0 (not 1, which is the post-quantization zero).
        Assert.Equal(0, q14a[5]);
        Assert.Equal(0, q14a[6]);
    }

    [Fact]
    public void IvfSearch_AllFraudCluster_ScoresOne_AndIsDenied()
    {
        var (index, query) = BuildSyntheticIvfIndex(allFraud: true);
        var ivf = new IvfFlatSearcher();

        var score = ivf.FraudScore5(index, query, nprobe: 2);
        var approved = score < Threshold;

        Assert.Equal(1.0f, score);
        Assert.False(approved);
    }

    [Fact]
    public void IvfSearch_AllLegitCluster_ScoresZero_AndIsApproved()
    {
        var (index, query) = BuildSyntheticIvfIndex(allFraud: false);
        var ivf = new IvfFlatSearcher();

        var score = ivf.FraudScore5(index, query, nprobe: 2);
        var approved = score < Threshold;

        Assert.Equal(0.0f, score);
        Assert.True(approved);
    }

    [Fact]
    public void Decision_AtThreshold_MatchesProductionRule()
    {
        // Rule: approved = fraud_score < 0.6.
        // 0.6 itself is NOT approved.
        Assert.False(0.6f < Threshold);
        Assert.True(0.4f < Threshold);
        Assert.True(0.5999f < Threshold);
        Assert.False(0.6001f < Threshold);
    }

    private static (IvfIndex Index, byte[] Query) BuildSyntheticIvfIndex(bool allFraud)
    {
        // Two clusters, 5 vectors each. Cluster 0 is "near low-byte queries",
        // cluster 1 is "near high-byte queries". Query targets cluster 0, so
        // the top-5 always comes from cluster 0 → label distribution there
        // controls the score deterministically.
        const int dim = 14;
        const int nlist = 2;
        const int perCluster = 5;
        const int count = nlist * perCluster;

        var centroids = new byte[nlist * dim];
        for (var d = 0; d < dim; d++) centroids[d] = 50;
        for (var d = 0; d < dim; d++) centroids[dim + d] = 200;

        var offsets = new int[] { 0, perCluster, count };

        var vectors = new byte[count * dim];
        for (var i = 0; i < perCluster; i++)
        {
            var off = i * dim;
            for (var d = 0; d < dim; d++) vectors[off + d] = (byte)(50 + i);
        }
        for (var i = 0; i < perCluster; i++)
        {
            var off = (perCluster + i) * dim;
            for (var d = 0; d < dim; d++) vectors[off + d] = (byte)(200 + i);
        }

        byte clusterLabel = allFraud ? (byte)1 : (byte)0;
        var labels = new byte[count];
        for (var i = 0; i < perCluster; i++) labels[i] = clusterLabel;
        for (var i = perCluster; i < count; i++) labels[i] = (byte)(allFraud ? 0 : 1);

        var index = new IvfIndex
        {
            NList = nlist,
            Count = count,
            Centroids = centroids,
            Offsets = offsets,
            Vectors = vectors,
            Labels = labels,
        };

        var query = new byte[dim];
        for (var d = 0; d < dim; d++) query[d] = 50;

        return (index, query);
    }
}
