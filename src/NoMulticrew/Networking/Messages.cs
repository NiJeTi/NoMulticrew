using Mirage.Serialization;

namespace NoMulticrew.Networking;

internal interface IMessage<out T>
    where T : struct, IMessage<T>
{
    byte ProtocolVersion { get; }

    void Read(NetworkReader reader);
    void Write(NetworkWriter writer);
}

internal struct MulticrewHello : IMessage<MulticrewHello>
{
    public byte ProtocolVersion { get; private set; }

    public MulticrewHello(byte protocolVersion)
    {
        ProtocolVersion = protocolVersion;
    }

    public void Read(NetworkReader reader)
    {
        ProtocolVersion = reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte(ProtocolVersion);
    }
}

internal struct MulticrewWelcome : IMessage<MulticrewWelcome>
{
    public byte ProtocolVersion { get; private set; }

    public MulticrewWelcome(byte protocolVersion)
    {
        ProtocolVersion = protocolVersion;
    }

    public void Read(NetworkReader reader)
    {
        ProtocolVersion = reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte(ProtocolVersion);
    }
}