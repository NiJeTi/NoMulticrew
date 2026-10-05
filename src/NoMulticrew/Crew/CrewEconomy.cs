using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewEconomy
{
    private sealed class Ledger
    {
        public required Player? Pilot { get; init; }

        public Dictionary<Player, float> Crew { get; } = [];
    }

    private const float ClaimLifetimeSeconds = 0.25f;
    private const float CreditThreshold = 0.01f;

    private static readonly AccessTools.FieldRef<Unit, Dictionary<PersistentID, float>?> DamageCreditRef =
        AccessTools.FieldRefAccess<Unit, Dictionary<PersistentID, float>?>("damageCredit");

    private readonly ServerSession _session;

    private readonly Dictionary<(PersistentID Aircraft, WeaponInfo Weapon), Player> _launchers = [];
    private readonly Dictionary<PersistentID, Queue<(Player? Claimant, float Time)>> _claims = [];
    private readonly Dictionary<(PersistentID Target, PersistentID Aircraft), Ledger> _ledger = [];
    private readonly Dictionary<Player, (float Allocation, float Score)> _escrow = [];

    private PersistentID _contextAircraft;
    private Player? _contextCrew;
    private bool _paying;
    private bool _missileScope;
    private Player? _sender;

    public CrewEconomy(ServerSession session)
    {
        _session = session;
    }

    public void Attributed(Player sender, Action action)
    {
        _sender = sender;

        try
        {
            action();
        }
        finally
        {
            _sender = null;
        }
    }

    public void OnLaunch(Unit owner, WeaponStation station)
    {
        if (owner is not Aircraft aircraft)
        {
            return;
        }

        var key = (aircraft.persistentID, station.WeaponInfo);
        var launcher = _sender;

        if (launcher != null)
        {
            Plugin.Logger.LogDebug(
                $"{station.WeaponInfo.weaponName} from {aircraft.persistentID} launched by "
                + launcher.GetDisplayName(PlayerNameContext.Other)
            );
            _launchers[key] = launcher;
        }
        else
        {
            _launchers.Remove(key);
        }
    }

    public void OnClaim(Unit claimer)
    {
        if (!_session.Crew.IsCrewed(claimer.persistentID))
        {
            return;
        }

        if (!_claims.TryGetValue(claimer.persistentID, out var queue))
        {
            queue = new Queue<(Player?, float)>();
            _claims[claimer.persistentID] = queue;
        }

        queue.Enqueue((_sender, Time.unscaledTime));
    }

    public bool EnterGunContext(PersistentID dealer)
    {
        if (_missileScope || _contextCrew != null || !_claims.TryGetValue(dealer, out var queue))
        {
            return false;
        }

        var now = Time.unscaledTime;

        while (queue.Count > 0)
        {
            var (claimant, time) = queue.Dequeue();
            if (now - time > ClaimLifetimeSeconds)
            {
                continue;
            }

            return Enter(dealer, claimant);
        }

        return false;
    }

    public bool EnterMissileContext(Missile missile)
    {
        var weapon = missile.GetWeaponInfo();

        return _contextCrew == null
            && _launchers.TryGetValue((missile.ownerID, weapon), out var launcher)
            && Enter(missile.ownerID, launcher);
    }

    public bool EnterMissileScope(Missile? missile)
    {
        if (_missileScope || _contextCrew != null)
        {
            return false;
        }

        _missileScope = true;

        if (missile != null)
        {
            EnterMissileContext(missile);
        }

        return true;
    }

    public void ExitMissileScope()
    {
        _missileScope = false;
        ExitContext();
    }

    public bool EnterHitContext(Unit shooter, WeaponInfo weapon)
    {
        if (_contextCrew != null || shooter is not Aircraft aircraft || !_session.Crew.IsCrewed(aircraft.persistentID))
        {
            return false;
        }

        var index = aircraft.weaponStations.FindIndex(x => x.WeaponInfo == weapon);

        return index >= 0 && Enter(aircraft.persistentID, _session.Crew.OccupantOwning(aircraft, index));
    }

    public void ExitContext()
    {
        _contextAircraft = PersistentID.None;
        _contextCrew = null;
    }

    public void OnDamage(Unit target, PersistentID dealer, float amount)
    {
        if (_contextCrew == null || dealer != _contextAircraft)
        {
            return;
        }

        var key = (target.persistentID, dealer);
        if (!_ledger.TryGetValue(key, out var ledger))
        {
            UnitRegistry.TryGetPersistentUnit(dealer, out var dealerUnit);
            ledger = new Ledger { Pilot = dealerUnit?.player };
            _ledger[key] = ledger;
        }

        ledger.Crew[_contextCrew] = ledger.Crew.GetValueOrDefault(_contextCrew) + amount;
    }

    public bool Reward(
        FactionHQ hq,
        Player player,
        Unit? target,
        float allocation,
        float score,
        FactionHQ.RewardType type
    )
    {
        if (_paying)
        {
            return false;
        }

        var aircraft = player.Aircraft;
        var occupants = aircraft != null ? _session.Crew.Occupants(aircraft.persistentID) : [];
        var portions = Attribute(player, target, type, occupants);

        if (occupants.Count == 0 && portions.Count == 1 && ReferenceEquals(portions[0].Earner, player))
        {
            return false;
        }

        Plugin.Logger.LogDebug(
            $"{type} reward {allocation:F0}/{score:F1} for {player.GetDisplayName(PlayerNameContext.Other)}: "
            + string.Join(", ", portions.Select(x => $"{x.Earner.GetDisplayName(PlayerNameContext.Other)} {x.Fraction:P0}"))
        );

        _paying = true;

        try
        {
            foreach (var (earner, fraction) in portions)
            {
                Share(hq, earner, player, occupants, target, allocation * fraction, score * fraction, type);
            }
        }
        finally
        {
            _paying = false;
        }

        return true;
    }

    public float PendingOf(Player player)
    {
        return _escrow.TryGetValue(player, out var held) ? held.Allocation : 0f;
    }

    public void Settle(Player crew, PersistentID aircraftId, bool forfeit)
    {
        if (forfeit)
        {
            foreach (var ledger in _ledger.Values)
            {
                ledger.Crew.Remove(crew);
            }

            foreach (var key in _launchers.Where(x => ReferenceEquals(x.Value, crew)).Select(x => x.Key).ToList())
            {
                _launchers.Remove(key);
            }

            foreach (var dealer in _claims.Keys.ToList())
            {
                _claims[dealer] = new Queue<(Player?, float)>(
                    _claims[dealer].Select(x => ReferenceEquals(x.Claimant, crew) ? (null, x.Time) : x)
                );
            }
        }

        if (!_escrow.Remove(crew, out var held))
        {
            return;
        }

        var recipient = forfeit ? PilotOf(aircraftId, crew) : crew;
        if (recipient == null || recipient.HQ == null)
        {
            Plugin.Logger.LogInfo($"Crew escrow of {held.Allocation:F0} dropped: nobody to receive it");
            return;
        }

        _paying = true;

        try
        {
            recipient.HQ.RewardPlayer(recipient, null, held.Allocation, held.Score, FactionHQ.RewardType.None);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to settle crew escrow: {e}");
            return;
        }
        finally
        {
            _paying = false;
        }

        Plugin.Logger.LogInfo(
            $"Crew escrow of {held.Allocation:F0} {(forfeit ? "forfeited to" : "paid to")} "
            + recipient.GetDisplayName(PlayerNameContext.Other)
        );

        if (!forfeit)
        {
            _session.Notify(crew, $"Crew earnings paid: +{held.Allocation:F0}", CrewCue.Select);
        }
    }

    public void PayAll()
    {
        foreach (var crew in _escrow.Keys.ToList())
        {
            Settle(crew, PersistentID.None, forfeit: false);
        }
    }

    private static Player? PilotOf(PersistentID aircraftId, Player crew)
    {
        return UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft)
            && aircraft.Player != null
            && !ReferenceEquals(aircraft.Player, crew)
            ? aircraft.Player
            : null;
    }

    private bool Enter(PersistentID aircraft, Player? crew)
    {
        if (crew == null)
        {
            return false;
        }

        _contextAircraft = aircraft;
        _contextCrew = crew;

        return true;
    }

    private List<(Player Earner, float Fraction)> Attribute(
        Player pilot,
        Unit? target,
        FactionHQ.RewardType type,
        List<Player> occupants
    )
    {
        if (type != FactionHQ.RewardType.Kill || target == null)
        {
            return [(pilot, 1f)];
        }

        var credit = DamageCreditRef(target);
        var grand = credit?.Values.Sum() ?? 0f;
        var total = 0f;
        var crew = new Dictionary<Player, float>();
        var counted = new Dictionary<PersistentID, float>();

        if (credit != null && grand > 0f)
        {
            foreach (var (key, value) in credit)
            {
                if (value / grand >= CreditThreshold
                    && UnitRegistry.TryGetPersistentUnit(key, out var unit)
                    && ReferenceEquals(unit.player, pilot))
                {
                    total += value;
                    counted[key] = value;
                }
            }
        }

        foreach (var key in _ledger.Keys.Where(x => x.Target == target.persistentID).ToList())
        {
            var ledger = _ledger[key];
            if (!ReferenceEquals(ledger.Pilot, pilot))
            {
                continue;
            }

            _ledger.Remove(key);

            var cap = counted.GetValueOrDefault(key.Aircraft);
            var sum = ledger.Crew.Values.Sum();
            var scale = sum > cap ? cap / sum : 1f;

            foreach (var (member, amount) in ledger.Crew)
            {
                crew[member] = crew.GetValueOrDefault(member) + amount * scale;
            }
        }

        if (total <= 0f || crew.Count == 0)
        {
            return [(pilot, 1f)];
        }

        var portions = new List<(Player, float)>();
        var rest = 1f;

        foreach (var (member, amount) in crew)
        {
            var fraction = Mathf.Min(amount / total, rest);
            portions.Add((member, fraction));
            rest -= fraction;
        }

        if (rest > 0f)
        {
            portions.Add((pilot, rest));
        }

        return portions;
    }

    private void Share(
        FactionHQ hq,
        Player earner,
        Player pilot,
        List<Player> occupants,
        Unit? target,
        float allocation,
        float score,
        FactionHQ.RewardType type
    )
    {
        var participants = new List<Player>(occupants.Count + 1) { pilot };
        participants.AddRange(occupants);

        var others = participants.Where(x => !ReferenceEquals(x, earner)).ToList();
        var share = ReferenceEquals(earner, pilot)
            ? Plugin.Settings.PilotOutboundShare.Value
            : Plugin.Settings.CrewOutboundShare.Value;

        if (!participants.Contains(earner) || others.Count == 0 || share <= 0f)
        {
            Pay(hq, earner, target, allocation, score, type);
            return;
        }

        Pay(hq, earner, target, allocation * (1f - share), score * (1f - share), type);

        foreach (var other in others)
        {
            Pay(hq, other, target, allocation * share / others.Count, score * share / others.Count, type);
        }
    }

    private void Pay(FactionHQ hq, Player player, Unit? target, float allocation, float score, FactionHQ.RewardType type)
    {
        if (allocation <= 0f && score <= 0f)
        {
            return;
        }

        var aircraftId = _session.Crew.AircraftOf(player);
        if (aircraftId == null)
        {
            hq.RewardPlayer(player, target, allocation, score, type);
            return;
        }

        var held = _escrow.GetValueOrDefault(player);
        _escrow[player] = (held.Allocation + allocation, held.Score + score);

        if (type != FactionHQ.RewardType.None)
        {
            NetworkSceneSingleton<MessageManager>.i.TargetCreditMessage(
                player.Owner,
                target != null ? target.persistentID : PersistentID.None,
                score,
                type
            );
        }

        _session.Crew.SendRoster(aircraftId.Value);
    }
}
