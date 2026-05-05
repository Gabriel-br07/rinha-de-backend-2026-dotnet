using FraudDetection.Api.Dtos;
using FraudDetection.Api.Options;

namespace FraudDetection.Api.Vectorization;

public sealed class TransactionVectorizer
{
    private readonly NormalizationOptions _n;
    private readonly MccRiskProvider _mcc;

    public TransactionVectorizer(NormalizationOptions normalization, MccRiskProvider mccRiskProvider)
    {
        _n = normalization;
        _mcc = mccRiskProvider;
    }

    public void VectorizeTo14(FraudScoreRequest req, Span<float> v14)
    {
        // 0 amount
        v14[0] = Clamp.Clamp01(req.transaction.amount / _n.max_amount);

        // 1 installments
        v14[1] = Clamp.Clamp01(req.transaction.installments / _n.max_installments);

        // 2 amount_vs_avg
        var avg = req.customer.avg_amount;
        float amountVsAvg;
        if (avg <= 0f)
        {
            amountVsAvg = 0f;
        }
        else
        {
            amountVsAvg = (req.transaction.amount / avg) / _n.amount_vs_avg_ratio;
        }
        v14[2] = Clamp.Clamp01(amountVsAvg);

        // 3 hour_of_day (UTC)
        var utc = req.transaction.requested_at.UtcDateTime;
        v14[3] = Clamp.Clamp01(utc.Hour / 23f);

        // 4 day_of_week (Monday=0..Sunday=6)
        var dow = ((int)utc.DayOfWeek + 6) % 7;
        v14[4] = Clamp.Clamp01(dow / 6f);

        // 5 minutes_since_last_tx
        // 6 km_from_last_tx
        if (req.last_transaction is null)
        {
            v14[5] = -1f;
            v14[6] = -1f;
        }
        else
        {
            var delta = req.transaction.requested_at - req.last_transaction.timestamp;
            var minutes = (float)delta.TotalMinutes;
            if (minutes < 0f) minutes = 0f;
            v14[5] = Clamp.Clamp01(minutes / _n.max_minutes);

            v14[6] = Clamp.Clamp01(req.last_transaction.km_from_current / _n.max_km);
        }

        // 7 km_from_home
        v14[7] = Clamp.Clamp01(req.terminal.km_from_home / _n.max_km);

        // 8 tx_count_24h
        v14[8] = Clamp.Clamp01(req.customer.tx_count_24h / _n.max_tx_count_24h);

        // 9 is_online
        v14[9] = req.terminal.is_online ? 1f : 0f;

        // 10 card_present
        v14[10] = req.terminal.card_present ? 1f : 0f;

        // 11 unknown_merchant
        v14[11] = IsKnownMerchant(req.merchant.id, req.customer.known_merchants) ? 0f : 1f;

        // 12 mcc_risk
        v14[12] = _mcc.GetRiskOrDefault(req.merchant.mcc);

        // 13 merchant_avg_amount
        v14[13] = Clamp.Clamp01(req.merchant.avg_amount / _n.max_merchant_avg_amount);
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

