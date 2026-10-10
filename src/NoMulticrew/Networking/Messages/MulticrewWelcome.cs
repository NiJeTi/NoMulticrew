using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct MulticrewWelcome : IMessage<MulticrewWelcome>
{
    public byte ProtocolVersion { get; private set; }

    public string PluginVersion { get; private set; }

    public MulticrewWelcome(byte protocolVersion, string pluginVersion)
    {
        ProtocolVersion = protocolVersion;
        PluginVersion = pluginVersion;
    }

    public void Read(NetworkReader reader)
    {
        ProtocolVersion = reader.ReadByte();
        PluginVersion = ProtocolVersion == MessageRegistry.ProtocolVersion ? reader.ReadString() : "";
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte(ProtocolVersion);
        writer.WriteString(PluginVersion);
    }
}