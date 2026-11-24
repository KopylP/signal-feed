// Одноразовий інтерактивний вхід у Telegram (телефон, код, 2FA) і завантаження сесії в Blob Storage.
//
//   dotnet run --project src/TgLogin -- <storage-account-name | connection-string> [container]
//
// Дані акаунта беруться зі змінних TELEGRAM_API_ID, TELEGRAM_API_HASH, TELEGRAM_PHONE,
// а якщо їх немає, програма запитає їх у консолі. У Azure доступ до Blob іде через
// DefaultAzureCredential (az login), для Azurite передайте рядок підключення.

using Azure.Identity;
using Azure.Storage.Blobs;
using WTelegram;

const string SessionBlobName = "wtelegram.session"; // те саме ім'я читає PostAgent.Functions

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TgLogin <storage-account-name | connection-string> [container=tg-session]");
    return 1;
}

var target = args[0];
var containerName = args.Length > 1 ? args[1] : "tg-session";

var container = target.Contains('=')
    ? new BlobContainerClient(target, containerName)
    : new BlobContainerClient(new Uri($"https://{target}.blob.core.windows.net/{containerName}"), new DefaultAzureCredential());

var session = new MemoryStream();
using (var client = new Client(Config, session))
{
    var me = await client.LoginUserIfNeeded();
    Console.WriteLine($"Logged in as {me}");
}

await container.CreateIfNotExistsAsync();
await container.GetBlobClient(SessionBlobName).UploadAsync(BinaryData.FromBytes(session.ToArray()), overwrite: true);
Console.WriteLine($"Session uploaded to {container.Uri}/{SessionBlobName}");
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
