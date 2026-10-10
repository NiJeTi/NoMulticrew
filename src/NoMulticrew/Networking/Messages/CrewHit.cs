using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewHit : IMessage<CrewHit>
{
    public PersistentID TargetId { get; private set; }

    public Vector3Compressed RelativePos { get; private set; }

    public CrewHit(PersistentID targetId, Vector3Compressed relativePos)
    {
        TargetId = targetId;
        RelativePos = relativePos;
    }

    public void Read(NetworkReader reader)
    {
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        RelativePos = reader.Read<Vector3Compressed>();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(TargetId.Id);
        writer.Write(RelativePos);
    }
}