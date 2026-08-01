using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Data;

public sealed class CosmosAuditRepository : IAuditRepository
{
    private readonly Container _container;

    public CosmosAuditRepository(CosmosClient cosmosClient, Configuration.CosmosSettings settings)
    {
        _container = cosmosClient.GetContainer(settings.DatabaseId, settings.ContainerId);
    }

    public async Task CreateAsync(ConversionAudit audit, CancellationToken cancellationToken)
    {
        await _container.CreateItemAsync(audit, new PartitionKey(audit.tenantId), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ConversionPreview>> ListLatestAsync(string tenantId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);

        var query = new QueryDefinition(
            "SELECT TOP @limit * FROM c WHERE c.tenantId = @tenant ORDER BY c.backendExecutionTimestampUtc DESC")
            .WithParameter("@limit", limit)
            .WithParameter("@tenant", tenantId);

        var results = new List<ConversionPreview>(Math.Min(limit, 50));
        using var iterator = _container.GetItemQueryIterator<ConversionAudit>(query);

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page.Resource.Select(ToPreview));
        }

        return results;
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
