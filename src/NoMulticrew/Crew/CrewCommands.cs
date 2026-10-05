using HarmonyLib;
using Mirage;
using Mirage.SocketLayer;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewCommands
{
    private delegate void StationTargetsHalf(Unit unit, byte stationIndex, ReadOnlySpan<PersistentID> targetIds);

    private sealed class Limit(int refill, int maxTokens, int penalty)
    {
        public RateLimitBucket.RefillConfig Config { get; } =
            new() { Interval = 1f, Refill = refill, MaxTokens = maxTokens };

        public int Penalty { get; } = penalty;
    }

    private const int NoAuthorityCost = 10;
    private const float ReleaseGraceSeconds = 1f;

    private static readonly Limit FireLimit = new(20, 100, 1);
    private static readonly Limit ClaimLimit = new(20, 400, 5);
    private static readonly Limit LaunchLimit = new(15, 45, 2);
    private static readonly Limit TurretLimit = new(20, 100, 1);
    private static readonly Limit TargetsLimit = new(10, 30, 2);

    private static readonly Action<Unit, byte>? SingleRemoteFire =
        Bind<Action<Unit, byte>>(typeof(Unit), "CmdSingleRemoteFire");

    private static readonly Action<Unit, byte>? StoppedFiring =
        Bind<Action<Unit, byte>>(typeof(Unit), "CmdStoppedFiring");

    private static readonly Action<Unit, PersistentID, Vector3Compressed, Vector3Compressed, byte>? ClaimHit =
        Bind<Action<Unit, PersistentID, Vector3Compressed, Vector3Compressed, byte>>(typeof(Unit), "CmdClaimHit");

    private static readonly Action<Aircraft, byte, Unit?, GlobalPosition>? LaunchMissile =
        Bind<Action<Aircraft, byte, Unit?, GlobalPosition>>(typeof(Aircraft), "CmdLaunchMissile");

    private static readonly Action<Aircraft, byte, Vector3Compressed>? SetTurretVector =
        Bind<Action<Aircraft, byte, Vector3Compressed>>(typeof(Aircraft), "CmdSetTurretVector");

    private static readonly StationTargetsHalf? SetStationTargets =
        Bind<StationTargetsHalf>(typeof(Unit), "CmdSetStationTargets");

    private readonly ServerSession _session;
    private readonly CrewRegistry _crew;
    private readonly CrewEconomy _economy;

    private readonly Dictionary<(INetworkPlayer Connection, CrewCommandKind Kind), RateLimitBucket> _buckets = [];
    private readonly Dictionary<PersistentID, int> _owned = [];
    private readonly Dictionary<PersistentID, int> _firing = [];
    private readonly Dictionary<INetworkPlayer, (PersistentID Aircraft, float Time)> _released = [];

    public CrewCommands(ServerSession session, CrewRegistry crew, CrewEconomy economy)
    {
        _session = session;
        _crew = crew;
        _economy = economy;
    }

    public void OnCommand(INetworkPlayer connection, CrewCommand message)
    {
        if (!_session.TryGetPlayer(connection, out var sender))
        {
            return;
        }

        if (!UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft)
            || aircraft.disabled
            || aircraft.Player == null)
        {
            Plugin.Logger.LogDebug($"Dropped crew {message.Kind} for {message.AircraftId}: no live piloted aircraft");
            return;
        }

        var name = sender.GetDisplayName(PlayerNameContext.Other);

        var seat = _crew.SeatOf(sender, message.AircraftId);
        if (seat == null)
        {
            if (_released.TryGetValue(connection, out var released)
                && released.Aircraft == message.AircraftId
                && Time.unscaledTime - released.Time <= ReleaseGraceSeconds)
            {
                Plugin.Logger.LogDebug($"Dropped late crew {message.Kind} for {message.AircraftId} from {name}");
                return;
            }

            Plugin.Logger.LogWarning($"Crew {message.Kind} for {message.AircraftId} from {name}, who has no seat in it");
            connection.SetError(NoAuthorityCost, PlayerErrorFlags.NoAuthority);
            return;
        }

        if (!CrewState.Owns(aircraft, seat.Value, message.Station))
        {
            Plugin.Logger.LogWarning(
                $"Crew {message.Kind} from {name} names station {message.Station} of {message.AircraftId}, "
                + "which their seat does not own"
            );
            connection.SetError(NoAuthorityCost, PlayerErrorFlags.NoAuthority);
            return;
        }

        if (!TryUseToken(connection, message.Kind))
        {
            return;
        }

        if (!Validate(message, out var cost, out var flags))
        {
            Plugin.Logger.LogWarning($"Crew {message.Kind} from {name} has invalid arguments");
            connection.SetError(cost, flags);
            return;
        }

        Execute(aircraft, sender, message);
    }

    public void MergeFiring(Unit unit, ref WeaponMask value)
    {
        if (!_owned.TryGetValue(unit.persistentID, out var owned))
        {
            return;
        }

        value = new WeaponMask((value.Mask & ~owned) | (_firing.GetValueOrDefault(unit.persistentID) & owned));
    }

    public void Reconcile(PersistentID aircraftId)
    {
        var before = _owned.GetValueOrDefault(aircraftId);
        var owned = 0;

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft);

        if (aircraft != null)
        {
            for (var i = 0; i < aircraft.weaponStations.Count && i < 32; i++)
            {
                if (_crew.OccupantOwning(aircraft, i) != null)
                {
                    owned |= 1 << i;
                }
            }
        }

        if (owned == 0)
        {
            _owned.Remove(aircraftId);
            _firing.Remove(aircraftId);
        }
        else
        {
            _owned[aircraftId] = owned;
            _firing[aircraftId] = _firing.GetValueOrDefault(aircraftId) & owned;
        }

        var dropped = before & ~owned;
        if (dropped != 0 && aircraft != null)
        {
            aircraft.NetworkremoteWeaponStates = new WeaponMask(aircraft.NetworkremoteWeaponStates.Mask & ~dropped);
        }

        if (dropped == 0 || aircraft == null || SetStationTargets == null)
        {
            return;
        }

        for (var i = 0; i < 32; i++)
        {
            if ((dropped & (1 << i)) != 0 && i < aircraft.weaponStations.Count)
            {
                SetStationTargets(aircraft, (byte)i, ReadOnlySpan<PersistentID>.Empty);
            }
        }
    }

    public void Released(Player player, PersistentID aircraftId)
    {
        _released[player.Owner] = (aircraftId, Time.unscaledTime);
    }

    public void Forget(INetworkPlayer connection)
    {
        _released.Remove(connection);

        foreach (var key in _buckets.Keys.Where(x => x.Connection == connection).ToList())
        {
            _buckets.Remove(key);
        }
    }

    public void Clear()
    {
        _buckets.Clear();
        _owned.Clear();
        _firing.Clear();
        _released.Clear();
    }

    private static T? Bind<T>(Type type, string name)
        where T : Delegate
    {
        var method = UserCode.Find(type, name);
        if (method == null)
        {
            Plugin.Logger.LogError($"{type.Name}.UserCode_{name} not found: crew commands of that kind are dropped");
            return null;
        }

        return AccessTools.MethodDelegate<T>(method);
    }

    private static Limit LimitOf(CrewCommandKind kind)
    {
        return kind switch
        {
            CrewCommandKind.ClaimHit => ClaimLimit,
            CrewCommandKind.LaunchMissile => LaunchLimit,
            CrewCommandKind.TurretVector => TurretLimit,
            CrewCommandKind.SetStationTargets => TargetsLimit,
            _ => FireLimit,
        };
    }

    private static bool Validate(CrewCommand message, out int cost, out PlayerErrorFlags flags)
    {
        cost = 1;
        flags = NuclearOptionPlayerErrorFlags.InvalidValue;

        return message.Kind switch
        {
            CrewCommandKind.ClaimHit => UnitRegistry.TryGetUnit(message.TargetId, out _)
                && NetworkFloatHelper.TryDecompress(message.Vector, out _, logErrors: false, null)
                && NetworkFloatHelper.TryDecompress(message.Velocity, out _, logErrors: false, null),
            CrewCommandKind.LaunchMissile => NetworkFloatHelper.Validate(message.Aimpoint, logErrors: false, null),
            CrewCommandKind.TurretVector => NetworkFloatHelper.Validate(message.Vector, logErrors: false, null),
            _ => true,
        };
    }

    private bool TryUseToken(INetworkPlayer connection, CrewCommandKind kind)
    {
        var limit = LimitOf(kind);
        var now = Time.unscaledTimeAsDouble;

        if (!_buckets.TryGetValue((connection, kind), out var bucket))
        {
            bucket = new RateLimitBucket(now, limit.Config);
            _buckets[(connection, kind)] = bucket;
        }

        if (!bucket.UseTokens(now, 1))
        {
            return true;
        }

        Plugin.Logger.LogWarning($"Crew {kind} rate limit exceeded by {connection}, dropping it [penalty={limit.Penalty}]");
        connection.SetError(limit.Penalty, PlayerErrorFlags.RateLimit);

        return false;
    }

    private void Execute(Aircraft aircraft, Player sender, CrewCommand message)
    {
        switch (message.Kind)
        {
            case CrewCommandKind.FiringState:
                SetFiring(aircraft, message.Station, message.Firing);
                break;
            case CrewCommandKind.SingleFire:
                SingleRemoteFire?.Invoke(aircraft, message.Station);
                break;
            case CrewCommandKind.StoppedFiring:
                StoppedFiring?.Invoke(aircraft, message.Station);
                break;
            case CrewCommandKind.ClaimHit:
                Attributed(
                    sender,
                    () => ClaimHit?.Invoke(aircraft, message.TargetId, message.Vector, message.Velocity, message.Station)
                );
                break;
            case CrewCommandKind.LaunchMissile:
                Launch(aircraft, sender, message);
                break;
            case CrewCommandKind.TurretVector:
                SetTurretVector?.Invoke(aircraft, message.Station, message.Vector);
                _session.SendToPlayer(
                    aircraft.Player.Owner,
                    new CrewTurretVector(aircraft.persistentID, message.Station, message.Vector)
                );
                break;
            case CrewCommandKind.SetStationTargets:
                SetStationTargets?.Invoke(aircraft, message.Station, message.Targets);
                break;
        }
    }

    private void Launch(Aircraft aircraft, Player sender, CrewCommand message)
    {
        if (LaunchMissile == null)
        {
            return;
        }

        UnitRegistry.TryGetUnit(message.TargetId, out var target);

        Attributed(sender, () => LaunchMissile(aircraft, message.Station, target, message.Aimpoint));

        if (!aircraft.LocalSim)
        {
            _session.SendToPlayer(
                aircraft.Player.Owner,
                new CrewLaunch(aircraft.persistentID, message.Station, message.TargetId, message.Aimpoint)
            );
        }
    }

    private void SetFiring(Aircraft aircraft, byte station, bool firing)
    {
        var id = aircraft.persistentID;
        var bits = _firing.GetValueOrDefault(id);

        _firing[id] = firing ? bits | (1 << station) : bits & ~(1 << station);

        aircraft.NetworkremoteWeaponStates = aircraft.NetworkremoteWeaponStates;
    }

    private void Attributed(Player sender, Action action)
    {
        _economy.Sender = sender;

        try
        {
            action();
        }
        finally
        {
            _economy.Sender = null;
        }
    }
}
