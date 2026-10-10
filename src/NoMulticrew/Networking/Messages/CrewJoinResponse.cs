using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

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