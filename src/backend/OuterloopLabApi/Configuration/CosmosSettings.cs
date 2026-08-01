namespace OuterloopLabApi.Configuration;

public sealed record CosmosSettings(
    string CosmosDbUri,
    string DatabaseId,
    string ContainerId,
    string CosmosAccountName,
    string ResourceGroupName,
    string Region,
    string ManagedIdentityClientId
)
{
    public static CosmosSettings FromEnvironment()
    {
        static string ReadRequired(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException($"Missing required environment variable: {key}");
            return v;
        }

        var cosmosDbUri = ReadRequired("COSMOS_DB_URI");
        var databaseId = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE") ?? "currency-conversion-db";
        var containerId = Environment.GetEnvironmentVariable("COSMOS_DB_CONTAINER") ?? "currencyconversion";
        var cosmosAccountName = ReadRequired("COSMOS_DB_ACCOUNT_NAME");
        var resourceGroupName = ReadRequired("COSMOS_DB_RESOURCE_GROUP");
        var region = Environment.GetEnvironmentVariable("COSMOS_DB_REGION") ?? "Central India";
        var managedIdentityClientId = ReadRequired("AZURE_MANAGED_IDENTITY_CLIENT_ID");

        return new CosmosSettings(
            cosmosDbUri,
            databaseId,
            containerId,
            cosmosAccountName,
            resourceGroupName,
            region,
            managedIdentityClientId);
    }
}
