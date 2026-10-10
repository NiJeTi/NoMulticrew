using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewJoinResult : IMessage<CrewJoinResult>
{
    public PersistentID AircraftId { get; private set; }
    public CrewJoinOutcome Outcome { get; private set; }

    public CrewJoinResult(PersistentID aircraftId, CrewJoinOutcome outcome)
    {
        AircraftId = aircraftId;
        Outcome = outcome;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Outcome = (CrewJoinOutcome)reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteByte((byte)Outcome);
    }
}