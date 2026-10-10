using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewCommand : IMessage<CrewCommand>
{
    public const int MaxTargets = 128;

    public CrewCommandKind Kind { get; private set; }

    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public bool Firing { get; private set; }

    public PersistentID TargetId { get; private set; }

    public Vector3Compressed Vector { get; private set; }

    public Vector3Compressed Velocity { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    public PersistentID[] Targets { get; private set; }

    private CrewCommand(CrewCommandKind kind, PersistentID aircraftId, sbyte station)
    {
        Kind = kind;
        AircraftId = aircraftId;
        Station = station;
        Targets = [];
    }

    public static CrewCommand FiringState(PersistentID aircraftId, sbyte station, bool firing)
    {
        return new CrewCommand(CrewCommandKind.FiringState, aircraftId, station) { Firing = firing };
    }

    public static CrewCommand SingleFire(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.SingleFire, aircraftId, station);
    }

    public static CrewCommand StoppedFiring(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.StoppedFiring, aircraftId, station);
    }

    public static CrewCommand ClaimHit(
        PersistentID aircraftId,
        sbyte station,
        PersistentID hitId,
        Vector3Compressed relativePos,
        Vector3Compressed velocity
    )
    {
        return new CrewCommand(CrewCommandKind.ClaimHit, aircraftId, station)
        {
            TargetId = hitId,
            Vector = relativePos,
            Velocity = velocity
        };
    }

    public static CrewCommand LaunchMissile(
        PersistentID aircraftId,
        sbyte station,
        PersistentID targetId,
        GlobalPosition aimpoint
    )
    {
        return new CrewCommand(CrewCommandKind.LaunchMissile, aircraftId, station)
        {
            TargetId = targetId,
            Aimpoint = aimpoint
        };
    }

    public static CrewCommand TurretVector(PersistentID aircraftId, sbyte station, Vector3Compressed direction)
    {
        return new CrewCommand(CrewCommandKind.TurretVector, aircraftId, station) { Vector = direction };
    }

    public static CrewCommand SetStationTargets(PersistentID aircraftId, sbyte station, PersistentID[] targets)
    {
        return new CrewCommand(CrewCommandKind.SetStationTargets, aircraftId, station) { Targets = targets };
    }

    public static CrewCommand SelectStation(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.SelectStation, aircraftId, station);
    }

    public void Read(NetworkReader reader)
    {
        Kind = (CrewCommandKind)reader.ReadByte();
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        Targets = [];

        switch (Kind)
        {
            case CrewCommandKind.FiringState:
                Firing = reader.ReadBoolean();
                break;
            case CrewCommandKind.SingleFire:
            case CrewCommandKind.StoppedFiring:
            case CrewCommandKind.SelectStation:
                break;
            case CrewCommandKind.ClaimHit:
                TargetId = new PersistentID { Id = reader.ReadUInt32() };
                Vector = reader.Read<Vector3Compressed>();
                Velocity = reader.Read<Vector3Compressed>();
                break;
            case CrewCommandKind.LaunchMissile:
                TargetId = new PersistentID { Id = reader.ReadUInt32() };
                Aimpoint = reader.ReadGlobalPosition();
                break;
            case CrewCommandKind.TurretVector:
                Vector = reader.Read<Vector3Compressed>();
                break;
            case CrewCommandKind.SetStationTargets:
                var count = reader.ReadByte();
                if (count > MaxTargets)
                {
                    throw new InvalidOperationException($"Crew target list of {count} exceeds {MaxTargets}.");
                }

                Targets = new PersistentID[count];
                for (var i = 0; i < count; i++)
                {
                    Targets[i] = new PersistentID { Id = reader.ReadUInt32() };
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown crew command {Kind}.");
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte((byte)Kind);
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);

        switch (Kind)
        {
            case CrewCommandKind.FiringState:
                writer.WriteBoolean(Firing);
                break;
            case CrewCommandKind.ClaimHit:
                writer.WriteUInt32(TargetId.Id);
                writer.Write(Vector);
                writer.Write(Velocity);
                break;
            case CrewCommandKind.LaunchMissile:
                writer.WriteUInt32(TargetId.Id);
                writer.WriteGlobalPosition(Aimpoint);
                break;
            case CrewCommandKind.TurretVector:
                writer.Write(Vector);
                break;
            case CrewCommandKind.SetStationTargets:
                if (Targets.Length > MaxTargets)
                {
                    throw new InvalidOperationException($"Crew target list of {Targets.Length} exceeds {MaxTargets}.");
                }

                writer.WriteByte((byte)Targets.Length);
                foreach (var target in Targets)
                {
                    writer.WriteUInt32(target.Id);
                }

                break;
        }
    }
}