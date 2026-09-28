namespace NoMulticrew.Networking;

internal readonly struct CrewHello(byte protocolVersion)
{
    public readonly byte ProtocolVersion = protocolVersion;
}

internal readonly struct CrewWelcome(byte protocolVersion, bool accepted)
{
    public readonly byte ProtocolVersion = protocolVersion;
    public readonly bool Accepted = accepted;
}
