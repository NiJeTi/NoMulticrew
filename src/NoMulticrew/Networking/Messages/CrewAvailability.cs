using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewAvailability : IMessage<CrewAvailability>
{
    public bool Accepting { get; private set; }

    public CrewAvailability(bool accepting)
    {
        Accepting = accepting;
    }

    public void Read(NetworkReader reader)
    {
        Accepting = reader.ReadBoolean();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteBoolean(Accepting);
    }
}