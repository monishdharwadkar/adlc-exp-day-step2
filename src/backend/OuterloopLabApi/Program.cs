using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using OuterloopLabApi.Configuration;
using OuterloopLabApi.Data;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;
using OuterloopLabApi.Services;
using Microsoft.Azure.Cosmos;
using Azure.ResourceManager.CosmosDB;

var builder = WebApplication.CreateBuilder(args);

var useInMemory = string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY"), "true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddHttpClient<ExternalCurrencyRateClient>();
builder.Services.AddSingleton<CurrencyConversionService>();

if (useInMemory)
{
    builder.Services.AddSingleton<IAuditRepository, InMemoryAuditRepository>();
}
else
{
    var cosmosSettings = CosmosSettings.FromEnvironment();
    builder.Services.AddSingleton(cosmosSettings);

    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = cosmosSettings.ManagedIdentityClientId
    });

    // Control plane (ARM) provisioning is best-effort.
    try
    {
        // If we cannot resolve subscription, ARM provisioning may be skipped.
        var armClient = new ArmClient(credential);
        dynamic armClientDyn = armClient;
        dynamic subscription = await armClientDyn.GetDefaultSubscriptionAsync();

        if (subscription is not null && subscription.HasValue)
        {
            var cosmosDbAccountId = $"/subscriptions/{subscription.Value.SubscriptionId}/resourceGroups/{cosmosSettings.ResourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosSettings.CosmosAccountName}";
            var account = ((dynamic)armClient).GetCosmosDBAccountResource(new ResourceIdentifier(cosmosDbAccountId));

            // SQL database + container provisioning (best-effort).
            await CosmosArmProvisioning.TryEnsureSqlDbAndContainerAsync(account, cosmosSettings.DatabaseId, cosmosSettings.ContainerId, cancellationToken: CancellationToken.None);
        }
    }
    catch
    {
        // Best-effort only.
    }

    // Data plane provisioning must run with token-based credentials and fail startup if create-if-not-exists fails.
    var cosmosClient = new CosmosClient(cosmosSettings.CosmosDbUri, credential);

    try
    {
        await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosSettings.DatabaseId, throughput: 400);

        var db = cosmosClient.GetDatabase(cosmosSettings.DatabaseId);
        var containerProperties = new ContainerProperties(cosmosSettings.ContainerId, "/tenantId");
        await db.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
    }
    catch
    {
        // Required behavior: token-authenticated data-plane create-if-not-exists failure must fail startup.
        throw;
    }

    builder.Services.AddSingleton(cosmosClient);
    builder.Services.AddSingleton<CosmosAuditRepository>();
    builder.Services.AddSingleton<IAuditRepository>(sp => sp.GetRequiredService<CosmosAuditRepository>());
}

var app = builder.Build();

app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/conversions", async (ConvertRequest request, CurrencyConversionService service, CancellationToken ct) =>
{
    try
    {
        var preview = await service.ConvertAsync(request, ct);
        return Results.Ok(preview);
    }
    catch (CurrencyConversionException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

app.MapGet("/api/conversions", async (int? limit, IAuditRepository repo, CancellationToken ct) =>
{
    // Use the registered audit repository (Cosmos or InMemory).
    var items = await repo.ListLatestAsync("default", limit ?? 25, ct);
    return Results.Ok(items);
});

app.Run();

static class CosmosArmProvisioning
{
    // Best-effort: errors should not prevent startup.
    public static async Task TryEnsureSqlDbAndContainerAsync(dynamic account, string databaseId, string containerId, CancellationToken cancellationToken)
    {
        // Managed Identity RBAC for ARM can differ from data-plane RBAC.
        // Control-plane provisioning here is intentionally best-effort and uses dynamic calls
        // to avoid tight coupling to specific CosmosDB ARM SDK model types.
        try
        {
            dynamic sqlDb = account.GetCosmosDBSqlDatabase(databaseId);

            // Some SDK versions accept only WaitUntil + CancellationToken for create/update.
            try
            {
                await sqlDb.CreateOrUpdateAsync(WaitUntil.Completed, cancellationToken);
            }
            catch
            {
                // Ignore: will attempt container provisioning anyway.
            }

            dynamic sqlContainer = sqlDb.GetCosmosDBSqlContainer(containerId);
            try
            {
                // Provide a minimal shape; the call may still fail depending on SDK version.
                await sqlContainer.CreateOrUpdateAsync(
                    new { 
                        options = new { partitionKey = new { paths = new[] { "/tenantId" } } }
                    },
                    WaitUntil.Completed,
                    cancellationToken);
            }
            catch
            {
                // Ignore ARM failures.
            }
        }
        catch
        {
            // Ignore ARM failures.
        }
    }
}
