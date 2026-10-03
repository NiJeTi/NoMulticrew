using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private const string SectionCrew = "Crew";

    public ConfigEntry<bool> RejectAllRequests { get; }

    private Settings(ConfigEntry<bool> rejectAllRequests)
    {
        RejectAllRequests = rejectAllRequests;
    }

    public static Settings Init(ConfigFile config)
    {
        var rejectAllRequests = config.Bind(
            SectionCrew,
            "RejectAllRequests",
            false,
            "Decline every crew request for your aircraft without asking"
        );

        return new Settings(rejectAllRequests);
    }
}