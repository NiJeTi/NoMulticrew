using HarmonyLib;
using Mirage;
using Mirage.SocketLayer;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Server;

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
    private static readonly Limit SelectLimit = new(10, 30, 2);

    private static readonly Action<Unit, byte> SingleRemoteFire = AccessTools.MethodDelegate<Action<Unit, byte>>(
        GameMembers.Method(typeof(Unit), "UserCode_CmdSingleRemoteFire_1708169300")
    );

    private static readonly Action<Unit, byte> StoppedFiring = AccessTools.MethodDelegate<Action<Unit, byte>>(
        GameMembers.Method(typeof(Unit), "UserCode_CmdStoppedFiring_-1238964004")
    );

    private static readonly Action<Unit, PersistentID, Vector3Compressed, Vector3Compressed, byte> ClaimHit =
        AccessTools.MethodDelegate<Action<Unit, PersistentID, Vector3Compressed, Vector3Compressed, byte>>(
            GameMembers.Method(typeof(Unit), "UserCode_CmdClaimHit_-1122942669")
        );

    private static readonly Action<Aircraft, byte, Unit?, GlobalPosition> LaunchMissile =
        AccessTools.MethodDelegate<Action<Aircraft, byte, Unit?, GlobalPosition>>(
            GameMembers.Method(typeof(Aircraft), "UserCode_CmdLaunchMissile_644415535")
        );

    private static readonly Action<Aircraft, byte, Vector3Compressed> SetTurretVector =
        AccessTools.MethodDelegate<Action<Aircraft, byte, Vector3Compressed>>(
            GameMembers.Method(typeof(Aircraft), "UserCode_CmdSetTurretVector_-1133982608")
        );

    private static readonly StationTargetsHalf SetStationTargets = AccessTools.MethodDelegate<StationTargetsHalf>(
        GameMembers.Method(typeof(Unit), "UserCode_CmdSetStationTargets_872088460")
    );

    private readonly ServerSession _session;

    private readonly Dictionary<(INetworkPlayer Connection, CrewCommandKind Kind), RateLimitBucket> _buckets = [];
    private readonly Dictionary<PersistentID, (int Owned, int Firing)> _masks = [];
    private readonly Dictionary<INetworkPlayer, (PersistentID Aircraft, float Time)> _released = [];

    public CrewCommands(ServerSession session)
    {
        _session = session;
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

        if (!ReferenceEquals(_session.Crew.WsoOf(message.AircraftId), sender))
        {
            if (_released.TryGetValue(connection, out var released)
                && released.Aircraft == message.AircraftId
                && Time.unscaledTime - released.Time <= ReleaseGraceSeconds)
            {
                Plugin.Logger.LogDebug(
                    $"Dropped late crew {message.Kind} for {message.AircraftId} "
                    + $"from {sender.GetDisplayName(PlayerNameContext.Other)}"
                );
                return;
            }

            Plugin.Logger.LogWarning(
                $"Crew {message.Kind} for {message.AircraftId} from {sender.GetDisplayName(PlayerNameContext.Other)}: "
                + "sender not seated"
            );
            connection.SetError(NoAuthorityCost, PlayerErrorFlags.NoAuthority);
            return;
        }

        if (!TryUseToken(connection, message.Kind))
        {
            return;
        }

        if (message.Kind == CrewCommandKind.SelectStation)
        {
            SelectStation(connection, sender, aircraft, message.Station);
            return;
        }

        if (!ReferenceEquals(_session.Crew.WsoHolding(aircraft, message.Station), sender))
        {
            if (Plugin.SeatTable.IsShared(aircraft))
            {
                Plugin.Logger.LogDebug(
                    $"Dropped crew {message.Kind} from {sender.GetDisplayName(PlayerNameContext.Other)} "
                    + $"for station {message.Station} of {message.AircraftId}: station no longer owned by sender's seat"
                );
                return;
            }

            Plugin.Logger.LogWarning(
                $"Crew {message.Kind} from {sender.GetDisplayName(PlayerNameContext.Other)} "
                + $"for station {message.Station} of {message.AircraftId}: station not owned by sender's seat"
            );
            connection.SetError(NoAuthorityCost, PlayerErrorFlags.NoAuthority);
            return;
        }

        if (!Validate(message, out var cost, out var flags))
        {
            Plugin.Logger.LogWarning(
                $"Crew {message.Kind} from {sender.GetDisplayName(PlayerNameContext.Other)} has invalid arguments"
            );
            connection.SetError(cost, flags);
            return;
        }

        Execute(aircraft, sender, message);
    }

    public void MergeFiring(Unit unit, ref WeaponMask value)
    {
        if (!_masks.TryGetValue(unit.persistentID, out var masks))
        {
            return;
        }

        value = new WeaponMask((value.Mask & ~masks.Owned) | (masks.Firing & masks.Owned));
    }

    public void Reconcile(PersistentID aircraftId)
    {
        var before = _masks.GetValueOrDefault(aircraftId).Owned;
        var owned = 0;

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft);

        if (aircraft != null)
        {
            for (var i = 0; i < aircraft.weaponStations.Count && i < 32; i++)
            {
                if (_session.Crew.WsoHolding(aircraft, i) != null)
                {
                    owned |= 1 << i;
                }
            }
        }

        if (owned == 0)
        {
            _masks.Remove(aircraftId);
        }
        else
        {
            _masks[aircraftId] = (owned, _masks.GetValueOrDefault(aircraftId).Firing & owned);
        }

        var dropped = before & ~owned;
        if (dropped != 0 && aircraft != null)
        {
            aircraft.NetworkremoteWeaponStates = new WeaponMask(aircraft.NetworkremoteWeaponStates.Mask & ~dropped);
        }

        if (dropped == 0 || aircraft == null)
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

    private static Limit LimitOf(CrewCommandKind kind)
    {
        return kind switch
        {
            CrewCommandKind.ClaimHit => ClaimLimit,
            CrewCommandKind.LaunchMissile => LaunchLimit,
            CrewCommandKind.TurretVector => TurretLimit,
            CrewCommandKind.SetStationTargets => TargetsLimit,
            CrewCommandKind.SelectStation => SelectLimit,
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

        Plugin.Logger.LogWarning(
            $"Crew {kind} rate limit exceeded by {connection}, dropping it [penalty={limit.Penalty}]"
        );
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
                SingleRemoteFire(aircraft, message.Station);
                break;
            case CrewCommandKind.StoppedFiring:
                StoppedFiring(aircraft, message.Station);
                break;
            case CrewCommandKind.ClaimHit:
                _session.Economy.Attributed(
                    sender,
                    () => ClaimHit(aircraft, message.TargetId, message.Vector, message.Velocity, message.Station)
                );
                break;
            case CrewCommandKind.LaunchMissile:
                Launch(aircraft, sender, message);
                break;
            case CrewCommandKind.TurretVector:
                SetTurretVector(aircraft, message.Station, message.Vector);
                _session.SendToPlayer(
                    aircraft.Player.Owner,
                    new CrewTurretVector(aircraft.persistentID, message.Station, message.Vector)
                );
                break;
            case CrewCommandKind.SetStationTargets:
                SetStationTargets(aircraft, message.Station, message.Targets);
                break;
        }
    }

    private void Launch(Aircraft aircraft, Player sender, CrewCommand message)
    {
        UnitRegistry.TryGetUnit(message.TargetId, out var target);

        _session.Economy.Attributed(sender, () => LaunchMissile(aircraft, message.Station, target, message.Aimpoint));

        if (!aircraft.LocalSim)
        {
            _session.SendToPlayer(
                aircraft.Player.Owner,
                new CrewLaunch(aircraft.persistentID, message.Station, message.TargetId, message.Aimpoint)
            );
        }
    }

    private void SelectStation(INetworkPlayer connection, Player sender, Aircraft aircraft, byte station)
    {
        if (station != SeatTable.NoStation && station >= aircraft.weaponStations.Count)
        {
            Plugin.Logger.LogWarning(
                $"Crew station {station} from {sender.GetDisplayName(PlayerNameContext.Other)} "
                + $"is out of bounds for {aircraft.persistentID}"
            );
            connection.SetError(1, NuclearOptionPlayerErrorFlags.OutOfBounds);
            return;
        }

        if (station != SeatTable.NoStation
            && !Plugin.SeatTable.CanSelect(aircraft, Role.Wso, station, _session.Crew.ServerState(aircraft)))
        {
            if (!Plugin.SeatTable.IsShared(aircraft))
            {
                Plugin.Logger.LogWarning(
                    $"Crew station {station} from {sender.GetDisplayName(PlayerNameContext.Other)} "
                    + $"is not owned by sender's seat on {aircraft.persistentID}"
                );
                connection.SetError(NoAuthorityCost, PlayerErrorFlags.NoAuthority);
                return;
            }

            Plugin.Logger.LogDebug(
                $"Refused station {station} of {aircraft.persistentID} "
                + $"to {sender.GetDisplayName(PlayerNameContext.Other)}: held by pilot"
            );
            _session.Crew.SendRoster(aircraft.persistentID);
            return;
        }

        _session.Crew.Select(aircraft.persistentID, station);
    }

    private void SetFiring(Aircraft aircraft, byte station, bool firing)
    {
        var id = aircraft.persistentID;
        var (owned, bits) = _masks.GetValueOrDefault(id);

        _masks[id] = (owned, firing ? bits | (1 << station) : bits & ~(1 << station));

        aircraft.NetworkremoteWeaponStates = aircraft.NetworkremoteWeaponStates;
    }
}