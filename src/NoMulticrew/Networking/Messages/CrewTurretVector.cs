using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewTurretVector : IMessage<CrewTurretVector>
{
    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public Vector3Compressed Direction { get; private set; }

    public CrewTurretVector(PersistentID aircraftId, sbyte station, Vector3Compressed direction)
    {
        AircraftId = aircraftId;
        Station = station;
        Direction = direction;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        Direction = reader.Read<Vector3Compressed>();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);
        writer.Write(Direction);
    }
}