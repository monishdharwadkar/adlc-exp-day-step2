namespace OuterloopLabApi.Models;

public sealed record CurrencyRateQuote(
    decimal Rate,
    string? ProviderDate
);
