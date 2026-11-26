using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Infrastructure;

public static class StorageRegistration
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config["AzureWebJobsStorage"];
        var account = config["AzureWebJobsStorage:accountName"];
        var queueOptions = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };

        if (IsLocal(config))
        {
            services.AddSingleton(new TableServiceClient(connectionString));
            services.AddSingleton(new BlobServiceClient(connectionString));
            services.AddSingleton(new QueueClient(connectionString, StorageNames.ScoringQueue, queueOptions));
            services.AddKeyedSingleton(StorageNames.ChatQueue, new QueueClient(connectionString, StorageNames.ChatQueue, queueOptions));
            services.AddKeyedSingleton(StorageNames.LoginQueue, new QueueClient(connectionString, StorageNames.LoginQueue, queueOptions));
            services.AddKeyedSingleton(StorageNames.DigestRequestQueue, new QueueClient(connectionString, StorageNames.DigestRequestQueue, queueOptions));
            services.AddKeyedSingleton(StorageNames.CleanupRequestQueue, new QueueClient(connectionString, StorageNames.CleanupRequestQueue, queueOptions));
        }
        else if (!string.IsNullOrEmpty(account))
        {
            var credential = new DefaultAzureCredential();
            services.AddSingleton(new TableServiceClient(new Uri($"https://{account}.table.core.windows.net"), credential));
            services.AddSingleton(new BlobServiceClient(new Uri($"https://{account}.blob.core.windows.net"), credential));
            services.AddSingleton(new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{StorageNames.ScoringQueue}"), credential, queueOptions));
            services.AddKeyedSingleton(StorageNames.ChatQueue, new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{StorageNames.ChatQueue}"), credential, queueOptions));
            services.AddKeyedSingleton(StorageNames.LoginQueue, new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{StorageNames.LoginQueue}"), credential, queueOptions));
            services.AddKeyedSingleton(StorageNames.DigestRequestQueue, new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{StorageNames.DigestRequestQueue}"), credential, queueOptions));
            services.AddKeyedSingleton(StorageNames.CleanupRequestQueue, new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{StorageNames.CleanupRequestQueue}"), credential, queueOptions));
        }
        else
        {
            throw new InvalidOperationException(
                "Set AzureWebJobsStorage (connection string, local) or AzureWebJobsStorage__accountName (Azure).");
        }

        return services;
    }

    // The identity-based account name wins: the platform can leave a connection string with an empty AccountKey
    // next to it, and building a client from that string stops the worker at startup.
    public static bool IsLocal(IConfiguration config) =>
        string.IsNullOrEmpty(config["AzureWebJobsStorage:accountName"]) && !string.IsNullOrEmpty(config["AzureWebJobsStorage"]);

    public static async Task EnsureLocalResourcesAsync(this IServiceProvider services, CancellationToken ct)
    {
        if (!IsLocal(services.GetRequiredService<IConfiguration>()))
        {
            return;
        }

        var tables = services.GetRequiredService<TableServiceClient>();
        await tables.CreateTableIfNotExistsAsync(StorageNames.PostsTable, ct);
        await tables.CreateTableIfNotExistsAsync(StorageNames.CursorsTable, ct);
        await tables.CreateTableIfNotExistsAsync(StorageNames.ChatBindingTable, ct);
        await services.GetRequiredService<QueueClient>().CreateIfNotExistsAsync(cancellationToken: ct);
        await services.GetRequiredKeyedService<QueueClient>(StorageNames.ChatQueue).CreateIfNotExistsAsync(cancellationToken: ct);
        await services.GetRequiredKeyedService<QueueClient>(StorageNames.LoginQueue).CreateIfNotExistsAsync(cancellationToken: ct);
        await services.GetRequiredKeyedService<QueueClient>(StorageNames.DigestRequestQueue).CreateIfNotExistsAsync(cancellationToken: ct);
        await services.GetRequiredKeyedService<QueueClient>(StorageNames.CleanupRequestQueue).CreateIfNotExistsAsync(cancellationToken: ct);

        var container = services.GetRequiredService<IOptions<AgentOptions>>().Value.SessionBlobContainer;
        var blobs = services.GetRequiredService<BlobServiceClient>();
        await blobs.GetBlobContainerClient(container).CreateIfNotExistsAsync(cancellationToken: ct);
        await blobs.GetBlobContainerClient(StorageNames.ChatStateContainer).CreateIfNotExistsAsync(cancellationToken: ct);
        await blobs.GetBlobContainerClient(StorageNames.SettingsContainer).CreateIfNotExistsAsync(cancellationToken: ct);
    }
}
