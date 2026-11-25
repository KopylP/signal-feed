using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Tests.Shared.Infrastructure;

public class StorageRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddStorage(config).BuildServiceProvider();
    }

    [Fact]
    public void Uses_the_account_name_even_when_a_connection_string_without_a_key_is_present()
    {
        // The Azure platform can leave a connection string with an empty AccountKey next to the identity-based setting.
        using var provider = Build(new()
        {
            ["AzureWebJobsStorage"] = "DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=;EndpointSuffix=core.windows.net",
            ["AzureWebJobsStorage:accountName"] = "acct",
        });

        var blobs = provider.GetRequiredService<BlobServiceClient>();

        Assert.Equal("acct", blobs.AccountName);
        Assert.Equal(new Uri("https://acct.blob.core.windows.net"), blobs.Uri);
    }

    [Fact]
    public void Uses_the_connection_string_when_there_is_no_account_name()
    {
        using var provider = Build(new() { ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true" });

        Assert.Equal("devstoreaccount1", provider.GetRequiredService<BlobServiceClient>().AccountName);
    }

    [Fact]
    public void Fails_when_neither_setting_is_present()
    {
        Assert.Throws<InvalidOperationException>(() => Build(new()));
    }
}
