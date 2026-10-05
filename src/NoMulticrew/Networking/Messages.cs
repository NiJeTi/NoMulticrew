using Mirage.Serialization;
using NoMulticrew.Seats;
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

    public SeatRole[] Roles { get; private set; }

    public CrewRoster(PersistentID aircraftId, int[] occupants, SeatRole[] roles)
    {
        AircraftId = aircraftId;
        Occupants = occupants;
        Roles = roles;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };

        var count = reader.ReadByte();

        Occupants = new int[count];
        Roles = new SeatRole[count];

        for (var i = 0; i < count; i++)
        {
            Occupants[i] = reader.ReadPackedInt32();
            Roles[i] = (SeatRole)reader.ReadByte();
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte((byte)Occupants.Length);

        for (var i = 0; i < Occupants.Length; i++)
        {
            writer.WritePackedInt32(Occupants[i]);
            writer.WriteByte((byte)Roles[i]);
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

internal enum CrewActionKind : byte
{
    Trigger = 0,
    Radar = 1,
    Aim = 2,
}

internal struct CrewAction : IMessage<CrewAction>
{
    public CrewActionKind Kind { get; private set; }

    public PersistentID AircraftId { get; private set; }

    public byte StationIndex { get; private set; }

    public bool Held { get; private set; }

    public PersistentID TargetId { get; private set; }

    public Vector3 Vector { get; private set; }

    private CrewAction(
        CrewActionKind kind,
        PersistentID aircraftId,
        byte stationIndex,
        bool held,
        PersistentID targetId,
        Vector3 vector
    )
    {
        Kind = kind;
        AircraftId = aircraftId;
        StationIndex = stationIndex;
        Held = held;
        TargetId = targetId;
        Vector = vector;
    }

    public static CrewAction Trigger(PersistentID aircraftId, byte stationIndex, bool held, PersistentID targetId)
    {
        return new CrewAction(CrewActionKind.Trigger, aircraftId, stationIndex, held, targetId, default);
    }

    public static CrewAction Radar(PersistentID aircraftId)
    {
        return new CrewAction(CrewActionKind.Radar, aircraftId, 0, false, PersistentID.None, default);
    }

    public static CrewAction Aim(PersistentID aircraftId, byte stationIndex, Vector3 vector)
    {
        return new CrewAction(CrewActionKind.Aim, aircraftId, stationIndex, false, PersistentID.None, vector);
    }

    public void Read(NetworkReader reader)
    {
        Kind = (CrewActionKind)reader.ReadByte();
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        StationIndex = reader.ReadByte();
        Held = reader.ReadBoolean();
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        Vector = reader.ReadVector3();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte((byte)Kind);
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte(StationIndex);
        writer.WriteBoolean(Held);
        writer.WriteUInt32(TargetId.Id);
        writer.WriteVector3(Vector);
    }
}
