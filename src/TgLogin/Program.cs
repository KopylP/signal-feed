// A one-off interactive Telegram sign-in (phone, code, 2FA) that uploads the session to Blob Storage.
//
//   dotnet run --project src/TgLogin -- <storage-account-name | connection-string> [container]
//
// The account data comes from the TELEGRAM_API_ID, TELEGRAM_API_HASH and TELEGRAM_PHONE variables;
// when they are missing, the program asks for them in the console. In Azure, Blob access goes through
// DefaultAzureCredential (az login); for Azurite pass a connection string.
// In Azure the session is encrypted with the Key Vault key from SESSION_KEY_ID (terraform output session_key_id, ADR-0017);
// against Azurite it may stay in clear.

using Azure.Identity;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.TelegramSession;
using WTelegram;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TgLogin <storage-account-name | connection-string> [container]");
    return 1;
}

var target = args[0];
var containerName = args.Length > 1 ? args[1] : StorageNames.DefaultSessionContainer;

var local = target.Contains('=');
var keyId = Environment.GetEnvironmentVariable("SESSION_KEY_ID");
if (!local && string.IsNullOrWhiteSpace(keyId))
{
    Console.Error.WriteLine("Set SESSION_KEY_ID=$(terraform -chdir=infra output -raw session_key_id): the session is stored encrypted.");
    return 1;
}

var credential = new DefaultAzureCredential();
var container = local
    ? new BlobContainerClient(target, containerName)
    : new BlobContainerClient(new Uri($"https://{target}.blob.core.windows.net/{containerName}"), credential);

var session = new MemoryStream();
using (var client = new Client(Config, session))
{
    var me = await client.LoginUserIfNeeded();
    Console.WriteLine($"Logged in as {me}");
}

var content = session.ToArray();
if (!string.IsNullOrWhiteSpace(keyId))
{
    var dataKey = SessionEnvelope.NewDataKey();
    var wrapped = await new KeyVaultKeyWrapper(keyId, credential).WrapAsync(dataKey, CancellationToken.None);
    content = SessionEnvelope.Seal(dataKey, wrapped, content, StorageNames.SessionBlob);
}

await container.CreateIfNotExistsAsync();
await container.GetBlobClient(StorageNames.SessionBlob).UploadAsync(BinaryData.FromBytes(content), overwrite: true);
Console.WriteLine($"Session uploaded to {container.Uri}/{StorageNames.SessionBlob}");
return 0;

static string? Config(string what) => what switch
{
    "api_id" => FromEnvOrAsk("TELEGRAM_API_ID", "API id"),
    "api_hash" => FromEnvOrAsk("TELEGRAM_API_HASH", "API hash"),
    "phone_number" => FromEnvOrAsk("TELEGRAM_PHONE", "Phone (+380...)"),
    "verification_code" => Ask("Code from Telegram"),
    "password" => AskSecret("2FA password"),
    _ => null,
};

static string FromEnvOrAsk(string variable, string prompt) =>
    Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : Ask(prompt);

static string Ask(string prompt)
{
    Console.Write($"{prompt}: ");
    return Console.ReadLine()?.Trim() ?? "";
}

static string AskSecret(string prompt)
{
    Console.Write($"{prompt}: ");
    var password = new System.Text.StringBuilder();
    while (Console.ReadKey(intercept: true) is var key && key.Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    return password.ToString();
}
