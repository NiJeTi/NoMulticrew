using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private const string SectionCrew = "Crew";
    private const string SectionEconomy = "Economy";
    private const string SectionDebug = "Debug";

    public ConfigEntry<bool> RejectAllRequests { get; }

    public ConfigEntry<float> PilotOutboundShare { get; }
    public ConfigEntry<float> CrewOutboundShare { get; }

    public ConfigEntry<bool> TintCrewmatePanel { get; }

    private Settings(
        ConfigEntry<bool> rejectAllRequests,
        ConfigEntry<float> pilotOutboundShare,
        ConfigEntry<float> crewOutboundShare,
        ConfigEntry<bool> tintCrewmatePanel
    )
    {
        RejectAllRequests = rejectAllRequests;
        PilotOutboundShare = pilotOutboundShare;
        CrewOutboundShare = crewOutboundShare;
        TintCrewmatePanel = tintCrewmatePanel;
    }

    public static Settings Init(ConfigFile config)
    {
        var rejectAllRequests = config.Bind(
            SectionCrew,
            "RejectAllRequests",
            false,
            "Decline every crew request for your aircraft without asking"
        );

        var pilotOutboundShare = config.Bind(
            SectionEconomy,
            "PilotOutboundShare",
            0.2f,
            new ConfigDescription(
                "Server: fraction of the pilot's earnings shared equally among the occupied crew seats",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );

        var crewOutboundShare = config.Bind(
            SectionEconomy,
            "CrewOutboundShare",
            0.2f,
            new ConfigDescription(
                "Server: fraction of a crew seat's earnings shared equally among the pilot and the other crew seats",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );

        var tintCrewmatePanel = config.Bind(
            SectionDebug,
            "TintCrewmatePanel",
            false,
            "Fill the crewmate's half of a side-by-side panel with solid magenta instead of their screen, to check where it sits"
        );

        return new Settings(rejectAllRequests, pilotOutboundShare, crewOutboundShare, tintCrewmatePanel);
    }
}