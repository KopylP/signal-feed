namespace PostAgent.Shared.Settings;

// Gone: already applied or declined (a second tap), nothing to show. Expired: the owner took too long.
public enum ProposalStatus
{
    Applied,
    Gone,
    Expired,
    Failed,
}
