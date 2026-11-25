using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Login.Infrastructure;

public sealed class WTelegramUserLoginFactory(IOptions<AgentOptions> options) : IUserLoginFactory
{
    public IUserLogin Create() => new WTelegramUserLogin(options.Value);
}
