using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private const string SectionGeneral = "General";
    private const string SectionServer = "Server";

    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<bool> AdvertiseCrewSupport { get; }
    public ConfigEntry<string> ForceCapableServers { get; }

    private Settings(
        ConfigEntry<bool> enabled,
        ConfigEntry<bool> advertiseCrewSupport,
        ConfigEntry<string> forceCapableServers
    )
    {
        Enabled = enabled;
        AdvertiseCrewSupport = advertiseCrewSupport;
        ForceCapableServers = forceCapableServers;
    }

    public static Settings Init(ConfigFile config)
    {
        var enabled = config.Bind(
            SectionGeneral, "Enabled", true,
            "Master switch. When false the plugin patches nothing and sends nothing."
        );
        var advertiseCrewSupport = config.Bind(
            SectionServer, "AdvertiseCrewSupport", true,
            "When hosting, tell the server browser this server supports multicrew. "
            + "Turning this off leaves crews unable to form."
        );
        var forceCapableServers = config.Bind(
            SectionServer, "ForceCapableServers", "",
            "Comma-separated Steam lobby or server IDs to treat as multicrew-capable "
            + "even when they do not advertise it. Escape hatch only."
        );

        return new Settings(enabled, advertiseCrewSupport, forceCapableServers);
    }
}
