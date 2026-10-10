using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    public ConfigEntry<bool> RejectAllRequests { get; }

    public Settings(ConfigFile config)
    {
        RejectAllRequests = config.Bind(
            "Crew",
            "RejectAllRequests",
            false,
            "Decline every crew request for your aircraft without asking"
        );
    }
}