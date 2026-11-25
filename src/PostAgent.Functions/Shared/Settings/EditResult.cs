namespace PostAgent.Shared.Settings;

public sealed record EditResult(bool Ok, string? Error = null)
{
    public static readonly EditResult Success = new(true);

    public static EditResult Fail(string error) => new(false, error);
}
