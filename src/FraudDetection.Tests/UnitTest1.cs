using FraudDetection.Api.Data;
using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;
using FraudDetection.Api.Search;
using FraudDetection.Api.Vectorization;

namespace FraudDetection.Tests;

public sealed class VectorizationTests
{
    [Fact]
    public void Vectorize_LastTransactionNull_PreservesMinusOne()
    {
        var n = new NormalizationOptions();
        var mcc = MccFromJson("{\"5411\":0.15}");
        var v = new TransactionVectorizer(n, mcc);

        var req = SampleRequest(lastTransaction: null);

        Span<float> vec = stackalloc float[14];
        v.VectorizeTo14(req, vec);

        Assert.Equal(-1f, vec[5]);
        Assert.Equal(-1f, vec[6]);
    }

    [Fact]
    public void Vectorize_KnownMerchant_UnknownMerchantIsZero()
    {
        var n = new NormalizationOptions();
        var mcc = MccFromJson("{\"5411\":0.15}");
        var v = new TransactionVectorizer(n, mcc);

        var req = SampleRequest(knownMerchants: ["MERC-001", "MERC-123"], merchantId: "MERC-123");

        Span<float> vec = stackalloc float[14];
        v.VectorizeTo14(req, vec);

        Assert.Equal(0f, vec[11]);
    }

    [Fact]
    public void Vectorize_UnknownMerchant_UnknownMerchantIsOne()
    {
        var n = new NormalizationOptions();
        var mcc = MccFromJson("{\"5411\":0.15}");
        var v = new TransactionVectorizer(n, mcc);

        var req = SampleRequest(knownMerchants: ["MERC-001", "MERC-002"], merchantId: "MERC-999");

        Span<float> vec = stackalloc float[14];
        v.VectorizeTo14(req, vec);

        Assert.Equal(1f, vec[11]);
    }

    [Fact]
    public void Vectorize_MccMissing_DefaultsToPointFive()
    {
        var n = new NormalizationOptions();
        var mcc = MccFromJson("{\"5411\":0.15}");
        var v = new TransactionVectorizer(n, mcc);

        var req = SampleRequest(mcc: "9999");

        Span<float> vec = stackalloc float[14];
        v.VectorizeTo14(req, vec);

        Assert.Equal(0.5f, vec[12]);
    }

    [Fact]
    public void Quantizer_EncodesSpecialValues()
    {
        Assert.Equal((byte)0, Quantizer.Encode(-1f));
        Assert.Equal((byte)1, Quantizer.Encode(0f));
        Assert.Equal((byte)255, Quantizer.Encode(1f));
    }

    private static FraudScoreRequest SampleRequest(
        LastTransactionDto? lastTransaction = null,
        string[]? knownMerchants = null,
        string merchantId = "MERC-001",
        string? mcc = "5411")
    {
        return new FraudScoreRequest(
            id: "tx-1",
            transaction: new TransactionDto(amount: 100f, installments: 2, requested_at: DateTimeOffset.Parse("2026-03-11T20:23:35Z")),
            customer: new CustomerDto(avg_amount: 200f, tx_count_24h: 3, known_merchants: knownMerchants ?? ["MERC-001"]),
            merchant: new MerchantDto(id: merchantId, mcc: mcc, avg_amount: 300f),
            terminal: new TerminalDto(is_online: false, card_present: true, km_from_home: 10f),
            last_transaction: lastTransaction
        );
    }

    private static MccRiskProvider MccFromJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcc_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return MccRiskProvider.LoadFromFile(path);
    }
}

public sealed class BinaryAndSearchTests
{
    [Fact]
    public void VectorIndexLoader_ThrowsWhenVectorSizeInvalid()
    {
        var dir = Directory.CreateTempSubdirectory("rinha-test-");
        var refs = Path.Combine(dir.FullName, "references.bin");
        var labels = Path.Combine(dir.FullName, "labels.bin");

        File.WriteAllBytes(refs, new byte[15]); // not divisible by 14
        File.WriteAllBytes(labels, new byte[1]);

        var paths = new DataPathsOptions
        {
            ReferencesBinPath = refs,
            LabelsBinPath = labels
        };

        Assert.Throws<InvalidDataException>(() => VectorIndexLoader.Load(paths));
    }

    [Fact]
    public void FraudScore_IsFraudNeighborsDividedByFive()
    {
        // 5 references; query matches first 5 exactly => all in top 5.
        // Make 4 of them fraud.
        var vectors = new byte[5 * 14];
        for (var i = 0; i < vectors.Length; i++) vectors[i] = 10;

        var labels = new byte[] { 1, 1, 1, 1, 0 };
        var index = new VectorIndex(vectors, labels);

        Span<byte> query = stackalloc byte[14];
        for (var i = 0; i < 14; i++) query[i] = 10;

        var s = new ExactKnnSearcher();
        var score = s.FraudScore5(index, query);

        Assert.Equal(0.8f, score);
    }
}

