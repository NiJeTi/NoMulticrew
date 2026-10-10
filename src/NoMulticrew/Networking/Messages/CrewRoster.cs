using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewRoster : IMessage<CrewRoster>
{
    public PersistentID AircraftId { get; private set; }

    public int WsoPlayerIndex { get; private set; }

    public float Pending { get; private set; }

    public sbyte WsoStation { get; private set; }

    public sbyte PilotStation { get; private set; }

    public CrewRoster(PersistentID aircraftId, int wsoPlayerIndex, float pending, sbyte wsoStation, sbyte pilotStation)
    {
        AircraftId = aircraftId;
        WsoPlayerIndex = wsoPlayerIndex;
        Pending = pending;
        WsoStation = wsoStation;
        PilotStation = pilotStation;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        WsoPlayerIndex = reader.ReadPackedInt32();
        Pending = reader.ReadSingleConverter();
        WsoStation = reader.ReadSByte();
        PilotStation = reader.ReadSByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WritePackedInt32(WsoPlayerIndex);
        writer.WriteSingleConverter(Pending);
        writer.WriteSByte(WsoStation);
        writer.WriteSByte(PilotStation);
    }
}