using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace PostAgent.Storage;

/// <summary>
/// Клієнти Table/Queue/Blob. В Azure: identity-based (AzureWebJobsStorage__accountName + managed identity).
/// Локально: рядок підключення AzureWebJobsStorage (Azurite).
/// </summary>
public static class StorageRegistration
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config["AzureWebJobsStorage"];
        var account = config["AzureWebJobsStorage:accountName"];
        var queueOptions = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddSingleton(new TableServiceClient(connectionString));
            services.AddSingleton(new BlobServiceClient(connectionString));
            services.AddSingleton(new QueueClient(connectionString, ScoringQueue.Name, queueOptions));
        }
        else if (!string.IsNullOrEmpty(account))
        {
            var credential = new DefaultAzureCredential();
            services.AddSingleton(new TableServiceClient(new Uri($"https://{account}.table.core.windows.net"), credential));
            services.AddSingleton(new BlobServiceClient(new Uri($"https://{account}.blob.core.windows.net"), credential));
            services.AddSingleton(new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{ScoringQueue.Name}"), credential, queueOptions));
        }
        else
        {
            throw new InvalidOperationException(
                "Set AzureWebJobsStorage (connection string, local) or AzureWebJobsStorage__accountName (Azure).");
        }

        return services;
    }

    /// <summary>Локально створює таблиці, чергу й контейнер. В Azure їх створює Terraform.</summary>
    public static async Task EnsureLocalResourcesAsync(this IServiceProvider services)
    {
        if (string.IsNullOrEmpty(services.GetRequiredService<IConfiguration>()["AzureWebJobsStorage"]))
        {
            return;
        }

        var tables = services.GetRequiredService<TableServiceClient>();
        await tables.CreateTableIfNotExistsAsync(PostRepository.PostsTable);
        await tables.CreateTableIfNotExistsAsync(SourceCursors.TableName);
        await services.GetRequiredService<QueueClient>().CreateIfNotExistsAsync();

        var container = services.GetRequiredService<IOptions<AgentOptions>>().Value.SessionBlobContainer;
        await services.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(container).CreateIfNotExistsAsync();
    }
}
