namespace PostAgent.Shared.Settings;

public sealed record ProposalOutcome(ProposalStatus Status, SettingsProposal? Proposal = null, string? Error = null);
