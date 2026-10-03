using Mirage.Serialization;

namespace NoMulticrew.Networking;

internal static class MessageRegistry
{
    public const byte ProtocolVersion = 2;

    public static void RegisterAll()
    {
        Register<MulticrewHello>();
        Register<MulticrewWelcome>();
        Register<MulticrewState>();
        Register<MulticrewJoinRequest>();
        Register<MulticrewJoinPrompt>();
        Register<MulticrewJoinResponse>();
        Register<MulticrewNotice>();
        Register<MulticrewLeaveRequest>();
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