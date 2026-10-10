using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal interface IMessage<out T>
    where T : struct, IMessage<T>
{
    void Read(NetworkReader reader);
    void Write(NetworkWriter writer);
}