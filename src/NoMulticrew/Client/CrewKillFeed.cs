using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Client;

internal sealed class CrewKillFeed
{
    private const float RecordLifetimeSeconds = 5f;

    private static readonly Func<KillType, PersistentUnit, PersistentUnit, bool> Filter =
        AccessTools.MethodDelegate<Func<KillType, PersistentUnit, PersistentUnit, bool>>(
            GameMembers.Method(typeof(MessageManager), "KillFeedFilter")
        );

    private static readonly Func<FactionHQ, Color> ColorFromFaction =
        AccessTools.MethodDelegate<Func<FactionHQ, Color>>(
            GameMembers.Method(typeof(MessageManager), "ColorFromFaction")
        );

    private readonly Dictionary<PersistentID, (int PlayerIndex, float Time)> _authors = [];

    public void Record(PersistentID killedId, int playerIndex)
    {
        var now = Time.unscaledTime;

        foreach (var stale in _authors.Where(x => now - x.Value.Time > RecordLifetimeSeconds).Select(x => x.Key))
        {
            _authors.Remove(stale);
        }

        _authors[killedId] = (playerIndex, now);
    }

    public bool TryPrint(PersistentID killerId, PersistentID killedId, KillType killedType)
    {
        if (!_authors.Remove(killedId, out var author)
            || Time.unscaledTime - author.Time > RecordLifetimeSeconds
            || !UnitRegistry.TryGetPersistentUnit(killedId, out var killed)
            || !UnitRegistry.TryGetPersistentUnit(killerId, out var killer))
        {
            return false;
        }

        if (!Shows(killedType, killer, killed, author.PlayerIndex))
        {
            return true;
        }

        var name = $"{CrewState.NameOf(author.PlayerIndex)} [{killer.definition.unitName}]";
        var line = name.AddColor(ColorFromFaction(killer.GetHQ()))
            + " "
            + killedType.GetVerb(true)
            + " "
            + killed.unitName.AddColor(ColorFromFaction(killed.GetHQ()));

        SceneSingleton<GameplayUI>.i.KillFeed(line);

        return true;
    }

    public void Clear()
    {
        _authors.Clear();
    }

    private static bool Shows(KillType killedType, PersistentUnit killer, PersistentUnit killed, int authorIndex)
    {
        if (killedType.GetFilterLevel() != PlayerSettings.KillFeedFilter.Player)
        {
            return Filter(killedType, killer, killed);
        }

        return killed.definition.value >= PlayerSettings.killFeedMinValue
            && ((GameManager.GetLocalPlayer<Player>(out var local) && local.PlayerIndex == authorIndex)
                || GameManager.IsLocalPlayer(killed.player));
    }
}