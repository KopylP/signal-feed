#:package Azure.Storage.Queues
#:package Azure.Identity
// File-based apps publish with Native AOT by default, which pulls in Microsoft.DotNet.ILCompiler. The tool only runs through `dotnet run`,
// and that package's audit warning (NU1903) is an error here.
#:property PublishAot=false

// Starts a job now instead of waiting for its schedule, by putting its name into the run-now queue.
//
//   dotnet run tools/run-now.cs -- <collect|digest|daily-summary> [--storage <account | connection string>] [--yes]
//
// --storage defaults to the AzureWebJobsStorage environment variable, then to Azurite. A bare account
// name signs in with the Azure CLI (az login) or Azure PowerShell, which needs "Storage Queue Data Contributor" on the account;
// no storage keys. A connection string is for Azurite only: never pass an account key on the command line.
// The job runs only if the app has ManualRunsEnabled=true. The command does not wait for the job:
// read the result in the function logs.
// Keep the queue name and the job names in sync with StorageNames.RunNowQueue and the IJob names (a test checks it).

using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;

const string queueName = "run-now";
string[] jobs = ["collect", "digest", "daily-summary"];
// Jobs that send a Telegram message every time. collect only does it when a source fails, but it moves read cursors.
string[] sendsToTelegram = ["digest"];

var storageIndex = Array.IndexOf(args, "--storage");
var valueOfStorage = storageIndex >= 0 ? storageIndex + 1 : -1; // not a job name
var job = args.Where((a, i) => i != valueOfStorage && !a.StartsWith("--", StringComparison.Ordinal))
    .FirstOrDefault()?.ToLowerInvariant();
if (job is null || !jobs.Contains(job))
{
    Console.Error.WriteLine($"Usage: run-now <{string.Join('|', jobs)}> [--storage <account | connection string>] [--yes]");
    return 1;
}

var storage = storageIndex >= 0 && storageIndex + 1 < args.Length
    ? args[storageIndex + 1]
    : Environment.GetEnvironmentVariable("AzureWebJobsStorage") is { Length: > 0 } fromEnv ? fromEnv : "UseDevelopmentStorage=true";

// The queue trigger expects Base64 text, like the app's own queue clients.
var options = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
var queue = storage.Contains('=')
    ? new QueueClient(storage, queueName, options)
    : new QueueClient(new Uri($"https://{storage}.queue.core.windows.net/{queueName}"), Credential(), options);

if (sendsToTelegram.Contains(job) && !args.Contains("--yes"))
{
    Console.Write($"'{job}' sends a real Telegram message and marks posts as sent. Continue? [y/N] ");
    if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
    {
        return 1;
    }
}

await queue.CreateIfNotExistsAsync();
await queue.SendMessageAsync(job);
Console.WriteLine($"Queued '{job}' in {queue.Uri}. It runs only if ManualRunsEnabled is true; see the function logs.");
return 0;

// Not DefaultAzureCredential: on a developer machine it probes managed identity first and waits for the IMDS timeout before it fails,
// unless AZURE_TOKEN_CREDENTIALS is set. The tool is run by hand, so the signed-in CLI is the only source it needs.
static TokenCredential Credential() => new ChainedTokenCredential(new AzureCliCredential(), new AzurePowerShellCredential());
