using Mirage.Serialization;

namespace NoMulticrew.Networking;

internal static class CrewSerializers
{
    public const byte ProtocolVersion = 1;

    private static bool _registered;

    public static void Register<T>(Action<NetworkWriter, T> write, Func<NetworkReader, T> read)
    {
        Writer<T>.Write = write;
        Reader<T>.Read = read;

        MessagePacker.RegisterMessage<T>();
    }

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        Register<CrewHello>(
            (writer, message) => writer.WriteByte(message.ProtocolVersion),
            reader => new CrewHello(reader.ReadByte())
        );

        Register<CrewWelcome>(
            (writer, message) =>
            {
                writer.WriteByte(message.ProtocolVersion);
                writer.WriteBoolean(message.Accepted);
            },
            reader => new CrewWelcome(reader.ReadByte(), reader.ReadBoolean())
        );

        Plugin.Logger.LogDebug($"Crew serializers registered, protocol {ProtocolVersion}");
    }
}
