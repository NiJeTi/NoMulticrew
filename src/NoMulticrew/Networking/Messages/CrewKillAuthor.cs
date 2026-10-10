using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewKillAuthor : IMessage<CrewKillAuthor>
{
    public PersistentID KilledId { get; private set; }

    public int PlayerIndex { get; private set; }

    public CrewKillAuthor(PersistentID killedId, int playerIndex)
    {
        KilledId = killedId;
        PlayerIndex = playerIndex;
    }

    public void Read(NetworkReader reader)
    {
        KilledId = new PersistentID { Id = reader.ReadUInt32() };
        PlayerIndex = reader.ReadPackedInt32();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(KilledId.Id);
        writer.WritePackedInt32(PlayerIndex);
    }
}