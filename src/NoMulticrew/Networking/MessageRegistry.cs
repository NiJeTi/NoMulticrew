using Mirage.Serialization;

namespace NoMulticrew.Networking;

internal static class MessageRegistry
{
    public const byte ProtocolVersion = 7;

    public static void RegisterAll()
    {
        Register<MulticrewHello>();
        Register<MulticrewWelcome>();
        Register<CrewRoster>();
        Register<CrewJoinRequest>();
        Register<CrewJoinPrompt>();
        Register<CrewJoinResponse>();
        Register<CrewNotice>();
        Register<CrewLeaveRequest>();
        Register<CrewCommand>();
        Register<CrewTurretVector>();
        Register<CrewLaunch>();
    }

    private static void Register<T>()
        where T : struct, IMessage<T>
    {
        Writer<T>.Write = (w, m) => m.Write(w);
        Reader<T>.Read = r =>
        {
            var m = new T();
            m.Read(r);
            return m;
        };

        MessagePacker.RegisterMessage<T>();
    }
}