using OuterloopLabApi.Models;

namespace OuterloopLabApi.Data;

public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly List<ConversionAudit> _items = new();
    private readonly object _lock = new();

    public Task CreateAsync(ConversionAudit audit, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _items.Add(audit);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversionPreview>> ListLatestAsync(string tenantId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);

        List<ConversionAudit> snapshot;
        lock (_lock)
        {
            snapshot = _items
                .Where(x => x.tenantId == tenantId)
                .OrderByDescending(x => x.backendExecutionTimestampUtc)
                .Take(limit)
                .ToList();
        }

        var previews = snapshot.Select(ToPreview).ToList();
        return Task.FromResult<IReadOnlyList<ConversionPreview>>(previews);
    }

    private static ConversionPreview ToPreview(ConversionAudit a)
    {
        return new ConversionPreview(
            AuditId: a.auditId,
            FromCurrency: a.fromCurrency,
            ToCurrency: a.toCurrency,
            InputAmount: a.inputAmount,
            Rate: a.rate,
            ConvertedAmount: a.convertedAmount,
            BackendExecutionTimestampUtc: a.backendExecutionTimestampUtc,
            ProviderDate: a.providerDate
        );
    }
}
