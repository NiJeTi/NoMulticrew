using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewLeaveRequest : IMessage<CrewLeaveRequest>
{
    public void Read(NetworkReader reader)
    {
    }

    public void Write(NetworkWriter writer)
    {
    }
}