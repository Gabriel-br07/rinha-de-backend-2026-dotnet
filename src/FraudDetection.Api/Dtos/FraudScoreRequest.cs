namespace FraudDetection.Api.Dtos;

public sealed record FraudScoreRequest(
    string? id,
    TransactionDto transaction,
    CustomerDto customer,
    MerchantDto merchant,
    TerminalDto terminal,
    LastTransactionDto? last_transaction
);

public sealed record TransactionDto(
    float amount,
    int installments,
    DateTimeOffset requested_at
);

public sealed record CustomerDto(
    float avg_amount,
    int tx_count_24h,
    string[]? known_merchants
);

public sealed record MerchantDto(
    string id,
    string? mcc,
    float avg_amount
);

public sealed record TerminalDto(
    bool is_online,
    bool card_present,
    float km_from_home
);

public sealed record LastTransactionDto(
    DateTimeOffset timestamp,
    float km_from_current
);

