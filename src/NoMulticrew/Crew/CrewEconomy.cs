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
    private const float LaunchLifetimeSeconds = 5f;
    private const float CreditThreshold = 0.01f;
    private const float PilotShare = 0.20f;
    private const float CrewShare = 0.35f;

    private static readonly AccessTools.FieldRef<Unit, Dictionary<PersistentID, float>?> DamageCreditRef =
        AccessTools.FieldRefAccess<Unit, Dictionary<PersistentID, float>?>("damageCredit");

    private readonly ServerSession _session;

    private readonly Dictionary<(PersistentID Aircraft, WeaponInfo Weapon), Queue<(Player? Launcher, float Time)>> _launches = [];
    private readonly Dictionary<Missile, Player> _launchers = [];
    private readonly Dictionary<PersistentID, Queue<(Player? Claimant, float Time)>> _claims = [];
    private readonly Dictionary<PersistentID, Dictionary<PersistentID, Ledger>> _ledger = [];
    private readonly Dictionary<Player, (float Allocation, float Score)> _escrow = [];

    private PersistentID _contextAircraft;
    private Player? _contextCrew;
    private bool _paying;
    private bool _missileScope;
    private Player? _sender;
    private (PersistentID Target, Player Author)? _killAuthor;

    public CrewEconomy(ServerSession session)
    {
        _session = session;
    }

    public Player? ContextCrew => _contextCrew;

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

    public void OnLaunch(Unit owner, WeaponStation station, int weaponIndex)
    {
        if (owner is not Aircraft aircraft
            || !_session.Crew.IsCrewed(aircraft.persistentID)
            || weaponIndex >= station.Weapons.Count
            || station.Weapons[weaponIndex] is not MountedMissile mounted
            || !mounted.IsAttached())
        {
            return;
        }

        var key = (aircraft.persistentID, station.WeaponInfo);

        if (!_launches.TryGetValue(key, out var queue))
        {
            queue = new Queue<(Player?, float)>();
            _launches[key] = queue;
        }

        queue.Enqueue((_sender, Time.unscaledTime));
    }

    public void OnSpawn(Missile missile, Unit? owner)
    {
        if (owner == null)
        {
            return;
        }

        var key = (owner.persistentID, missile.GetWeaponInfo());
        if (!_launches.TryGetValue(key, out var queue))
        {
            return;
        }

        var now = Time.unscaledTime;
        Player? launcher = null;

        while (queue.Count > 0)
        {
            var (next, time) = queue.Dequeue();
            if (now - time <= LaunchLifetimeSeconds)
            {
                launcher = next;
                break;
            }
        }

        if (queue.Count == 0)
        {
            _launches.Remove(key);
        }

        if (launcher == null)
        {
            return;
        }

        foreach (var spent in _launchers.Keys.Where(x => x == null).ToList())
        {
            _launchers.Remove(spent);
        }

        Plugin.Logger.LogDebug(
            $"{missile.GetWeaponInfo().weaponName} from {owner.persistentID} launched by "
            + launcher.GetDisplayName(PlayerNameContext.Other)
        );
        _launchers[missile] = launcher;
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
        Player? claimant = null;

        while (queue.Count > 0)
        {
            var (next, time) = queue.Dequeue();
            if (now - time <= ClaimLifetimeSeconds)
            {
                claimant = next;
                break;
            }
        }

        if (queue.Count == 0)
        {
            _claims.Remove(dealer);
        }

        return Enter(dealer, claimant);
    }

    public bool EnterMissileContext(Missile missile)
    {
        return _contextCrew == null
            && _launchers.TryGetValue(missile, out var launcher)
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

        return index >= 0 && Enter(aircraft.persistentID, _session.Crew.Holder(aircraft, index));
    }

    public void ExitContext()
    {
        _contextAircraft = PersistentID.None;
        _contextCrew = null;
    }

    public bool OpenKill(Unit target)
    {
        PersistentUnit? top = null;
        var most = 0f;

        foreach (var entry in Counted(target))
        {
            if (entry.Value >= most)
            {
                most = entry.Value;
                top = entry.Unit;
            }
        }

        if (top?.player is { } pilot)
        {
            var (total, crew) = Credit(target, pilot);

            if (crew.Count > 0)
            {
                var pilotOwn = total - crew.Values.Sum();
                var (member, amount) = crew.Aggregate((best, next) => next.Value > best.Value ? next : best);

                if (amount > pilotOwn)
                {
                    Plugin.Logger.LogDebug(
                        $"Kill of {target.persistentID} authored by {member.GetDisplayName(PlayerNameContext.Other)}: "
                        + $"{amount:F1} against the pilot's {pilotOwn:F1}"
                    );

                    _killAuthor = (target.persistentID, member);
                }
            }
        }

        return _ledger.ContainsKey(target.persistentID);
    }

    public void CloseKill(Unit target)
    {
        _killAuthor = null;
        _ledger.Remove(target.persistentID);
    }

    public Player? KillAuthorOf(PersistentID killedId)
    {
        return _killAuthor is { } held && held.Target == killedId ? held.Author : null;
    }

    public void OnDamage(Unit target, PersistentID dealer, float amount)
    {
        if (_contextCrew == null || dealer != _contextAircraft)
        {
            return;
        }

        if (!_ledger.TryGetValue(target.persistentID, out var ledgers))
        {
            ledgers = [];
            _ledger[target.persistentID] = ledgers;
        }

        if (!ledgers.TryGetValue(dealer, out var ledger))
        {
            UnitRegistry.TryGetPersistentUnit(dealer, out var dealerUnit);
            ledger = new Ledger { Pilot = dealerUnit?.player };
            ledgers[dealer] = ledger;
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
        var portions = Attribute(player, target, type);

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
            foreach (var (target, ledgers) in _ledger.ToList())
            {
                foreach (var (aircraft, ledger) in ledgers.ToList())
                {
                    ledger.Crew.Remove(crew);

                    if (ledger.Crew.Count == 0)
                    {
                        ledgers.Remove(aircraft);
                    }
                }

                if (ledgers.Count == 0)
                {
                    _ledger.Remove(target);
                }
            }

            foreach (var missile in _launchers.Where(x => ReferenceEquals(x.Value, crew)).Select(x => x.Key).ToList())
            {
                _launchers.Remove(missile);
            }

            foreach (var key in _launches.Keys.ToList())
            {
                _launches[key] = new Queue<(Player?, float)>(
                    _launches[key].Select(x => ReferenceEquals(x.Launcher, crew) ? (null, x.Time) : x)
                );
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

    private static List<(PersistentID Dealer, float Value, PersistentUnit Unit)> Counted(Unit target)
    {
        var counted = new List<(PersistentID, float, PersistentUnit)>();
        var credit = DamageCreditRef(target);
        var grand = credit?.Values.Sum() ?? 0f;

        if (credit == null || grand <= 0f)
        {
            return counted;
        }

        foreach (var (dealer, value) in credit)
        {
            if (UnitRegistry.TryGetPersistentUnit(dealer, out var unit) && value / grand >= CreditThreshold)
            {
                counted.Add((dealer, value, unit));
            }
        }

        return counted;
    }

    private (float Total, Dictionary<Player, float> Crew) Credit(Unit target, Player pilot)
    {
        var counted = Counted(target).Where(x => ReferenceEquals(x.Unit.player, pilot)).ToList();
        var total = counted.Sum(x => x.Value);
        var crew = new Dictionary<Player, float>();

        if (!_ledger.TryGetValue(target.persistentID, out var ledgers))
        {
            return (total, crew);
        }

        foreach (var (aircraft, ledger) in ledgers)
        {
            if (!ReferenceEquals(ledger.Pilot, pilot))
            {
                continue;
            }

            var cap = counted.FirstOrDefault(x => x.Dealer == aircraft).Value;
            var sum = ledger.Crew.Values.Sum();
            var scale = sum > cap ? cap / sum : 1f;

            foreach (var (member, amount) in ledger.Crew)
            {
                if (amount * scale > 0f)
                {
                    crew[member] = crew.GetValueOrDefault(member) + amount * scale;
                }
            }
        }

        return (total, crew);
    }

    private List<(Player Earner, float Fraction)> Attribute(Player pilot, Unit? target, FactionHQ.RewardType type)
    {
        if (type != FactionHQ.RewardType.Kill || target == null)
        {
            return [(pilot, 1f)];
        }

        var (total, crew) = Credit(target, pilot);

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
        var share = ReferenceEquals(earner, pilot) ? PilotShare : CrewShare;

        if (!participants.Contains(earner) || others.Count == 0)
        {
            Pay(hq, earner, target, allocation, score, type);
            return;
        }

        Pay(hq, earner, target, allocation * (1f - share), score * (1f - share), type);

        foreach (var other in others)
        {
            Pay(
                hq,
                other,
                target,
                allocation * share / others.Count,
                score * share / others.Count,
                FactionHQ.RewardType.None
            );
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
