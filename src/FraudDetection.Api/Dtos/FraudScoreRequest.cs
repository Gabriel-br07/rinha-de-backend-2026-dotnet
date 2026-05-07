namespace FraudDetection.Api.Dtos;

public readonly record struct FraudScoreRequest(
    string? id,
    TransactionDto transaction,
    CustomerDto customer,
    MerchantDto merchant,
    TerminalDto terminal,
    LastTransactionDto? last_transaction
);

public readonly record struct TransactionDto(
    float amount,
    int installments,
    DateTimeOffset requested_at
);

public readonly record struct CustomerDto(
    float avg_amount,
    int tx_count_24h,
    string[]? known_merchants
);

public readonly record struct MerchantDto(
    string id,
    string? mcc,
    float avg_amount
);

public readonly record struct TerminalDto(
    bool is_online,
    bool card_present,
    float km_from_home
);

public readonly record struct LastTransactionDto(
    DateTimeOffset timestamp,
    float km_from_current
);

