using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewLaunch : IMessage<CrewLaunch>
{
    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public PersistentID TargetId { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    public CrewLaunch(PersistentID aircraftId, sbyte station, PersistentID targetId, GlobalPosition aimpoint)
    {
        AircraftId = aircraftId;
        Station = station;
        TargetId = targetId;
        Aimpoint = aimpoint;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        Aimpoint = reader.ReadGlobalPosition();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);
        writer.WriteUInt32(TargetId.Id);
        writer.WriteGlobalPosition(Aimpoint);
    }
}