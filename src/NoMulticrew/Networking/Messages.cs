using Mirage.Serialization;
using NoMulticrew.Seats;

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

internal struct MulticrewState : IMessage<MulticrewState>
{
    public PersistentID AircraftId { get; private set; }

    public int[] Occupants { get; private set; }

    public SeatRole[] Roles { get; private set; }

    public MulticrewState(PersistentID aircraftId, int[] occupants, SeatRole[] roles)
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

internal struct MulticrewJoinRequest : IMessage<MulticrewJoinRequest>
{
    public PersistentID AircraftId { get; private set; }

    public byte SeatIndex { get; private set; }

    public MulticrewJoinRequest(PersistentID aircraftId, byte seatIndex)
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

internal struct MulticrewJoinPrompt : IMessage<MulticrewJoinPrompt>
{
    public int RequestId { get; private set; }

    public int JoinerPlayerIndex { get; private set; }

    public byte SeatIndex { get; private set; }

    public float ExpiresInSeconds { get; private set; }

    public MulticrewJoinPrompt(int requestId, int joinerPlayerIndex, byte seatIndex, float expiresInSeconds)
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

internal struct MulticrewJoinResponse : IMessage<MulticrewJoinResponse>
{
    public int RequestId { get; private set; }

    public bool Accepted { get; private set; }

    public MulticrewJoinResponse(int requestId, bool accepted)
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

internal struct MulticrewNotice : IMessage<MulticrewNotice>
{
    public string Text { get; private set; }

    public MulticrewNotice(string text)
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

internal struct MulticrewLeaveRequest : IMessage<MulticrewLeaveRequest>
{
    public void Read(NetworkReader reader)
    {
    }

    public void Write(NetworkWriter writer)
    {
    }
}