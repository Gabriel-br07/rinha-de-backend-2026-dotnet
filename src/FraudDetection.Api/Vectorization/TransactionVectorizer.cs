using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;

namespace FraudDetection.Api.Vectorization;

public sealed class TransactionVectorizer
{
    // Reciprocals so the request hot path multiplies instead of dividing.
    // Only safe because every divisor in NormalizationOptions is fixed at startup.
    private readonly float _invMaxAmount;
    private readonly float _invMaxInstallments;
    private readonly float _invAmountVsAvgRatio;
    private readonly float _invMaxMinutes;
    private readonly float _invMaxKm;
    private readonly float _invMaxTxCount24h;
    private readonly float _invMaxMerchantAvgAmount;
    private const float InvMaxHour = 1f / 23f;
    private const float InvMaxDayOfWeek = 1f / 6f;

    private readonly MccRiskProvider _mcc;

    public TransactionVectorizer(NormalizationOptions normalization, MccRiskProvider mccRiskProvider)
    {
        _invMaxAmount = RequirePositiveReciprocal(normalization.max_amount, nameof(normalization.max_amount));
        _invMaxInstallments = RequirePositiveReciprocal(normalization.max_installments, nameof(normalization.max_installments));
        _invAmountVsAvgRatio = RequirePositiveReciprocal(normalization.amount_vs_avg_ratio, nameof(normalization.amount_vs_avg_ratio));
        _invMaxMinutes = RequirePositiveReciprocal(normalization.max_minutes, nameof(normalization.max_minutes));
        _invMaxKm = RequirePositiveReciprocal(normalization.max_km, nameof(normalization.max_km));
        _invMaxTxCount24h = RequirePositiveReciprocal(normalization.max_tx_count_24h, nameof(normalization.max_tx_count_24h));
        _invMaxMerchantAvgAmount = RequirePositiveReciprocal(normalization.max_merchant_avg_amount, nameof(normalization.max_merchant_avg_amount));

        _mcc = mccRiskProvider;
    }

    public void VectorizeTo14(in FraudScoreRequest req, Span<float> v14)
    {
        v14[0] = Clamp.Clamp01(req.transaction.amount * _invMaxAmount);

        v14[1] = Clamp.Clamp01(req.transaction.installments * _invMaxInstallments);

        var avg = req.customer.avg_amount;
        float amountVsAvg;
        if (avg <= 0f)
        {
            amountVsAvg = 0f;
        }
        else
        {
            amountVsAvg = req.transaction.amount / avg * _invAmountVsAvgRatio;
        }
        v14[2] = Clamp.Clamp01(amountVsAvg);

        var utc = req.transaction.requested_at.UtcDateTime;
        v14[3] = Clamp.Clamp01(utc.Hour * InvMaxHour);

        var dow = ((int)utc.DayOfWeek + 6) % 7;
        v14[4] = Clamp.Clamp01(dow * InvMaxDayOfWeek);

        if (req.last_transaction is null)
        {
            v14[5] = -1f;
            v14[6] = -1f;
        }
        else
        {
            var last = req.last_transaction.Value;
            var delta = req.transaction.requested_at - last.timestamp;
            var minutes = (float)delta.TotalMinutes;
            if (minutes < 0f) minutes = 0f;
            v14[5] = Clamp.Clamp01(minutes * _invMaxMinutes);

            v14[6] = Clamp.Clamp01(last.km_from_current * _invMaxKm);
        }

        v14[7] = Clamp.Clamp01(req.terminal.km_from_home * _invMaxKm);

        v14[8] = Clamp.Clamp01(req.customer.tx_count_24h * _invMaxTxCount24h);

        v14[9] = req.terminal.is_online ? 1f : 0f;

        v14[10] = req.terminal.card_present ? 1f : 0f;

        v14[11] = IsKnownMerchant(req.merchant.id, req.customer.known_merchants) ? 0f : 1f;

        v14[12] = _mcc.GetRiskOrDefault(req.merchant.mcc);

        v14[13] = Clamp.Clamp01(req.merchant.avg_amount * _invMaxMerchantAvgAmount);
    }

    private static float RequirePositiveReciprocal(float value, string fieldName)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new InvalidOperationException($"{fieldName} must be a finite value greater than 0 (got {value}). Fix normalization.json.");
        return 1f / value;
    }

    private static bool IsKnownMerchant(string merchantId, string[]? knownMerchants)
    {
        if (knownMerchants is null || knownMerchants.Length == 0) return false;

        for (var i = 0; i < knownMerchants.Length; i++)
        {
            if (knownMerchants[i] == merchantId) return true;
        }

        return false;
    }
}

