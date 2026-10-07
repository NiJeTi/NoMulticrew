using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private const string SectionEconomy = "Economy";

    public ConfigEntry<bool> RejectAllRequests { get; }

    public ConfigEntry<float> PilotOutboundShare { get; }
    public ConfigEntry<float> CrewOutboundShare { get; }

    public Settings(ConfigFile config)
    {
        RejectAllRequests = config.Bind(
            "Crew",
            "RejectAllRequests",
            false,
            "Decline every crew request for your aircraft without asking"
        );

        PilotOutboundShare = config.Bind(
            SectionEconomy,
            "PilotOutboundShare",
            0.2f,
            new ConfigDescription(
                "Server: fraction of the pilot's earnings shared equally among the occupied crew seats",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );

        CrewOutboundShare = config.Bind(
            SectionEconomy,
            "CrewOutboundShare",
            0.2f,
            new ConfigDescription(
                "Server: fraction of a crew seat's earnings shared equally among the pilot and the other crew seats",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );
    }
}
