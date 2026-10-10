using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewJoinRequest : IMessage<CrewJoinRequest>
{
    public PersistentID AircraftId { get; private set; }

    public CrewJoinRequest(PersistentID aircraftId)
    {
        AircraftId = aircraftId;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
    }
}