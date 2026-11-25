namespace PostAgent.Shared.Settings;

// A button outside the settings menu can still lead into it: the collection failure message offers to remove a source, and the chat sends a proposal with its buttons.
// The data format lives here so that Collecting and the menu agree on it without referencing each other.
public static class MenuCallbacks
{
    public const string RemoveSourcePrefix = "sd:";

    public const string ApplyProposalPrefix = "pa:";
    public const string DeclineProposalPrefix = "pn:";

    public static string RemoveSource(string name) => RemoveSourcePrefix + name;

    public static string ApplyProposal(string id) => ApplyProposalPrefix + id;

    public static string DeclineProposal(string id) => DeclineProposalPrefix + id;
}
