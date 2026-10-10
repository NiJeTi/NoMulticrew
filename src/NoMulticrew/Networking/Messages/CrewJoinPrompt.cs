using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewJoinPrompt : IMessage<CrewJoinPrompt>
{
    public int RequestId { get; private set; }

    public int JoinerPlayerIndex { get; private set; }

    public float ExpiresInSeconds { get; private set; }

    public CrewJoinPrompt(int requestId, int joinerPlayerIndex, float expiresInSeconds)
    {
        RequestId = requestId;
        JoinerPlayerIndex = joinerPlayerIndex;
        ExpiresInSeconds = expiresInSeconds;
    }

    public void Read(NetworkReader reader)
    {
        RequestId = reader.ReadPackedInt32();
        JoinerPlayerIndex = reader.ReadPackedInt32();
        ExpiresInSeconds = reader.ReadSingleConverter();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WritePackedInt32(RequestId);
        writer.WritePackedInt32(JoinerPlayerIndex);
        writer.WriteSingleConverter(ExpiresInSeconds);
    }
}