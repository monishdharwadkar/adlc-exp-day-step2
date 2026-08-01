using OuterloopLabApi.Models;

namespace OuterloopLabApi.Data;

public interface IAuditRepository
{
    Task CreateAsync(ConversionAudit audit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversionPreview>> ListLatestAsync(string tenantId, int limit, CancellationToken cancellationToken);
}
