namespace PostAgent.Shared.Settings;

// A button outside the settings menu can still lead into it: the collection failure message offers to remove a source.
// The data format lives here so that Collecting and the menu agree on it without referencing each other.
public static class MenuCallbacks
{
    public const string RemoveSourcePrefix = "sd:";

    public static string RemoveSource(string name) => RemoveSourcePrefix + name;
}
