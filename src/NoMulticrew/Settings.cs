using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private const string SectionCrew = "Crew";
    private const string SectionEconomy = "Economy";

    public ConfigEntry<bool> RejectAllRequests { get; }

    public ConfigEntry<float> PilotOutboundShare { get; }
    public ConfigEntry<float> CrewOutboundShare { get; }

    private Settings(
        ConfigEntry<bool> rejectAllRequests,
        ConfigEntry<float> pilotOutboundShare,
        ConfigEntry<float> crewOutboundShare
    )
    {
        RejectAllRequests = rejectAllRequests;
        PilotOutboundShare = pilotOutboundShare;
        CrewOutboundShare = crewOutboundShare;
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

        return new Settings(rejectAllRequests, pilotOutboundShare, crewOutboundShare);
    }
}