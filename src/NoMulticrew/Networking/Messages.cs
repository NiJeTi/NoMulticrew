using Mirage;
using Mirage.Serialization;
using UnityEngine;

namespace NoMulticrew.Networking;

internal interface IMessage<out T>
    where T : struct, IMessage<T>
{
    void Read(NetworkReader reader);
    void Write(NetworkWriter writer);
}

internal struct MulticrewHello : IMessage<MulticrewHello>
{
    public byte ProtocolVersion { get; private set; }

    public MulticrewHello(byte protocolVersion)
    {
        ProtocolVersion = protocolVersion;
    }

    public void Read(NetworkReader reader)
    {
        ProtocolVersion = reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte(ProtocolVersion);
    }
}

internal struct MulticrewWelcome : IMessage<MulticrewWelcome>
{
    public byte ProtocolVersion { get; private set; }

    public MulticrewWelcome(byte protocolVersion)
    {
        ProtocolVersion = protocolVersion;
    }

    public void Read(NetworkReader reader)
    {
        ProtocolVersion = reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte(ProtocolVersion);
    }
}

internal struct CrewRoster : IMessage<CrewRoster>
{
    public PersistentID AircraftId { get; private set; }

    public int[] Occupants { get; private set; }

    public CrewRoster(PersistentID aircraftId, int[] occupants)
    {
        AircraftId = aircraftId;
        Occupants = occupants;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };

        var count = reader.ReadByte();

        Occupants = new int[count];

        for (var i = 0; i < count; i++)
        {
            Occupants[i] = reader.ReadPackedInt32();
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte((byte)Occupants.Length);

        for (var i = 0; i < Occupants.Length; i++)
        {
            writer.WritePackedInt32(Occupants[i]);
        }
    }
}

internal struct CrewJoinRequest : IMessage<CrewJoinRequest>
{
    public PersistentID AircraftId { get; private set; }

    public byte SeatIndex { get; private set; }

    public CrewJoinRequest(PersistentID aircraftId, byte seatIndex)
    {
        AircraftId = aircraftId;
        SeatIndex = seatIndex;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        SeatIndex = reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte(SeatIndex);
    }
}

internal struct CrewJoinPrompt : IMessage<CrewJoinPrompt>
{
    public int RequestId { get; private set; }

    public int JoinerPlayerIndex { get; private set; }

    public byte SeatIndex { get; private set; }

    public float ExpiresInSeconds { get; private set; }

    public CrewJoinPrompt(int requestId, int joinerPlayerIndex, byte seatIndex, float expiresInSeconds)
    {
        RequestId = requestId;
        JoinerPlayerIndex = joinerPlayerIndex;
        SeatIndex = seatIndex;
        ExpiresInSeconds = expiresInSeconds;
    }

    public void Read(NetworkReader reader)
    {
        RequestId = reader.ReadPackedInt32();
        JoinerPlayerIndex = reader.ReadPackedInt32();
        SeatIndex = reader.ReadByte();
        ExpiresInSeconds = reader.ReadSingleConverter();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WritePackedInt32(RequestId);
        writer.WritePackedInt32(JoinerPlayerIndex);
        writer.WriteByte(SeatIndex);
        writer.WriteSingleConverter(ExpiresInSeconds);
    }
}

internal struct CrewJoinResponse : IMessage<CrewJoinResponse>
{
    public int RequestId { get; private set; }

    public bool Accepted { get; private set; }

    public CrewJoinResponse(int requestId, bool accepted)
    {
        RequestId = requestId;
        Accepted = accepted;
    }

    public void Read(NetworkReader reader)
    {
        RequestId = reader.ReadPackedInt32();
        Accepted = reader.ReadBoolean();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WritePackedInt32(RequestId);
        writer.WriteBoolean(Accepted);
    }
}

internal struct CrewNotice : IMessage<CrewNotice>
{
    public string Text { get; private set; }

    public CrewNotice(string text)
    {
        Text = text;
    }

    public void Read(NetworkReader reader)
    {
        Text = reader.ReadString();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteString(Text);
    }
}

internal struct CrewLeaveRequest : IMessage<CrewLeaveRequest>
{
    public void Read(NetworkReader reader)
    {
    }

    public void Write(NetworkWriter writer)
    {
    }
}

internal enum CrewCommandKind : byte
{
    FiringState = 0,
    SingleFire = 1,
    StoppedFiring = 2,
    ClaimHit = 3,
    LaunchMissile = 4,
    TurretVector = 5,
}

internal struct CrewCommand : IMessage<CrewCommand>
{
    public CrewCommandKind Kind { get; private set; }

    public PersistentID AircraftId { get; private set; }

    public byte Station { get; private set; }

    public bool Firing { get; private set; }

    public PersistentID TargetId { get; private set; }

    public Vector3Compressed Vector { get; private set; }

    public Vector3Compressed Velocity { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    private CrewCommand(CrewCommandKind kind, PersistentID aircraftId, byte station)
    {
        Kind = kind;
        AircraftId = aircraftId;
        Station = station;
        TargetId = PersistentID.None;
    }

    public static CrewCommand FiringState(PersistentID aircraftId, byte station, bool firing)
    {
        return new CrewCommand(CrewCommandKind.FiringState, aircraftId, station) { Firing = firing };
    }

    public static CrewCommand SingleFire(PersistentID aircraftId, byte station)
    {
        return new CrewCommand(CrewCommandKind.SingleFire, aircraftId, station);
    }

    public static CrewCommand StoppedFiring(PersistentID aircraftId, byte station)
    {
        return new CrewCommand(CrewCommandKind.StoppedFiring, aircraftId, station);
    }

    public static CrewCommand ClaimHit(
        PersistentID aircraftId,
        byte station,
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
        byte station,
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

    public static CrewCommand TurretVector(PersistentID aircraftId, byte station, Vector3Compressed direction)
    {
        return new CrewCommand(CrewCommandKind.TurretVector, aircraftId, station) { Vector = direction };
    }

    public void Read(NetworkReader reader)
    {
        Kind = (CrewCommandKind)reader.ReadByte();
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadByte();
        TargetId = PersistentID.None;

        switch (Kind)
        {
            case CrewCommandKind.FiringState:
                Firing = reader.ReadBoolean();
                break;
            case CrewCommandKind.SingleFire:
            case CrewCommandKind.StoppedFiring:
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
            default:
                throw new InvalidOperationException($"Unknown crew command {Kind}");
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte((byte)Kind);
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte(Station);

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
        }
    }
}

internal struct CrewTurretVector : IMessage<CrewTurretVector>
{
    public PersistentID AircraftId { get; private set; }

    public byte Station { get; private set; }

    public Vector3Compressed Direction { get; private set; }

    public CrewTurretVector(PersistentID aircraftId, byte station, Vector3Compressed direction)
    {
        AircraftId = aircraftId;
        Station = station;
        Direction = direction;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadByte();
        Direction = reader.Read<Vector3Compressed>();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte(Station);
        writer.Write(Direction);
    }
}

internal struct CrewLaunch : IMessage<CrewLaunch>
{
    public PersistentID AircraftId { get; private set; }

    public byte Station { get; private set; }

    public PersistentID TargetId { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    public CrewLaunch(PersistentID aircraftId, byte station, PersistentID targetId, GlobalPosition aimpoint)
    {
        AircraftId = aircraftId;
        Station = station;
        TargetId = targetId;
        Aimpoint = aimpoint;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadByte();
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        Aimpoint = reader.ReadGlobalPosition();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte(Station);
        writer.WriteUInt32(TargetId.Id);
        writer.WriteGlobalPosition(Aimpoint);
    }
}
