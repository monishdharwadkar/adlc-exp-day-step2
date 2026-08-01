using OuterloopLabApi.Data;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;

namespace OuterloopLabApi.Services;

public sealed class CurrencyConversionService
{
    private readonly ExternalCurrencyRateClient _rateClient;
    private readonly IAuditRepository _repository;

    public CurrencyConversionService(ExternalCurrencyRateClient rateClient, IAuditRepository repository)
    {
        _rateClient = rateClient;
        _repository = repository;
    }

    public async Task<ConversionPreview> ConvertAsync(ConvertRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new CurrencyConversionException("Amount must be greater than zero.");

        var from = request.FromCurrency.Trim().ToUpperInvariant();
        var to = request.ToCurrency.Trim().ToUpperInvariant();

        CurrencyRateQuote quote;
        try
        {
            quote = await _rateClient.GetRateAsync(from, to, cancellationToken);
        }
        catch (CurrencyRateProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CurrencyRateProviderException($"Rate provider call failed: {ex.Message}");
        }

        // Exact backend execution timestamp for reconstructable audits.
        var backendExecutionTimestampUtc = DateTimeOffset.UtcNow;
        var converted = decimal.Round(request.Amount * quote.Rate, 6, MidpointRounding.AwayFromZero);

        var auditId = Guid.NewGuid().ToString("N");
        var audit = new ConversionAudit
        {
            auditId = auditId,
            tenantId = "default",
            fromCurrency = from,
            toCurrency = to,
            inputAmount = request.Amount,
            convertedAmount = converted,
            backendExecutionTimestampUtc = backendExecutionTimestampUtc,
            providerDate = quote.ProviderDate
        };

        // Persist only after a successful provider response.
        await _repository.CreateAsync(audit, cancellationToken);

        return new ConversionPreview(
            AuditId: auditId,
            FromCurrency: from,
            ToCurrency: to,
            InputAmount: request.Amount,
            Rate: quote.Rate,
            ConvertedAmount: converted,
            BackendExecutionTimestampUtc: backendExecutionTimestampUtc,
            ProviderDate: quote.ProviderDate
        );
    }
}
