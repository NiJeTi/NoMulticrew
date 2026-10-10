using Mirage.Serialization;

namespace NoMulticrew.Networking;

internal interface IMessage<out T>
    where T : struct, IMessage<T>
{
    void Read(NetworkReader reader);
    void Write(NetworkWriter writer);
}

internal struct MulticrewHello : IMessage<MulticrewHello>
{
    public byte ProtocolVersion { get; private set; }

    public string PluginVersion { get; private set; }

    public MulticrewHello(byte protocolVersion, string pluginVersion)
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

internal struct CrewRoster : IMessage<CrewRoster>
{
    public PersistentID AircraftId { get; private set; }

    public int WsoPlayerIndex { get; private set; }

    public float Pending { get; private set; }

    public sbyte WsoStation { get; private set; }

    public sbyte PilotStation { get; private set; }

    public CrewRoster(PersistentID aircraftId, int wsoPlayerIndex, float pending, sbyte wsoStation, sbyte pilotStation)
    {
        AircraftId = aircraftId;
        WsoPlayerIndex = wsoPlayerIndex;
        Pending = pending;
        WsoStation = wsoStation;
        PilotStation = pilotStation;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        WsoPlayerIndex = reader.ReadPackedInt32();
        Pending = reader.ReadSingleConverter();
        WsoStation = reader.ReadSByte();
        PilotStation = reader.ReadSByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WritePackedInt32(WsoPlayerIndex);
        writer.WriteSingleConverter(Pending);
        writer.WriteSByte(WsoStation);
        writer.WriteSByte(PilotStation);
    }
}

internal struct CrewJoinRequest : IMessage<CrewJoinRequest>
{
    public PersistentID AircraftId { get; private set; }

    public CrewJoinRequest(PersistentID aircraftId)
    {
        AircraftId = aircraftId;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
    }
}

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

internal struct CrewJoinResponse : IMessage<CrewJoinResponse>
{
    public int RequestId { get; private set; }

    public bool Accepted { get; private set; }

    public CrewJoinResponse(int requestId, bool accepted)
    {
        RequestId = requestId;
        Accepted = accepted;
    }

    public void Read(NetworkReader reader)
    {
        RequestId = reader.ReadPackedInt32();
        Accepted = reader.ReadBoolean();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WritePackedInt32(RequestId);
        writer.WriteBoolean(Accepted);
    }
}

internal enum CrewCue : byte
{
    None = 0,
    Select = 1,
    Deselect = 2,
    WeaponSwitch = 3,
}

internal struct CrewNotice : IMessage<CrewNotice>
{
    public string Text { get; private set; }
    public CrewCue Cue { get; private set; }

    public CrewNotice(string text, CrewCue cue = CrewCue.None)
    {
        Text = text;
        Cue = cue;
    }

    public void Read(NetworkReader reader)
    {
        Text = reader.ReadString();
        Cue = (CrewCue)reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteString(Text);
        writer.WriteByte((byte)Cue);
    }
}

internal struct CrewLeaveRequest : IMessage<CrewLeaveRequest>
{
    public void Read(NetworkReader reader)
    {
    }

    public void Write(NetworkWriter writer)
    {
    }
}

internal enum CrewCommandKind : byte
{
    FiringState = 0,
    SingleFire = 1,
    StoppedFiring = 2,
    ClaimHit = 3,
    LaunchMissile = 4,
    TurretVector = 5,
    SetStationTargets = 6,
    SelectStation = 7,
}

internal struct CrewCommand : IMessage<CrewCommand>
{
    public const int MaxTargets = 128;

    public CrewCommandKind Kind { get; private set; }

    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public bool Firing { get; private set; }

    public PersistentID TargetId { get; private set; }

    public Vector3Compressed Vector { get; private set; }

    public Vector3Compressed Velocity { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    public PersistentID[] Targets { get; private set; }

    private CrewCommand(CrewCommandKind kind, PersistentID aircraftId, sbyte station)
    {
        Kind = kind;
        AircraftId = aircraftId;
        Station = station;
        Targets = [];
    }

    public static CrewCommand FiringState(PersistentID aircraftId, sbyte station, bool firing)
    {
        return new CrewCommand(CrewCommandKind.FiringState, aircraftId, station) { Firing = firing };
    }

    public static CrewCommand SingleFire(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.SingleFire, aircraftId, station);
    }

    public static CrewCommand StoppedFiring(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.StoppedFiring, aircraftId, station);
    }

    public static CrewCommand ClaimHit(
        PersistentID aircraftId,
        sbyte station,
        PersistentID hitId,
        Vector3Compressed relativePos,
        Vector3Compressed velocity
    )
    {
        return new CrewCommand(CrewCommandKind.ClaimHit, aircraftId, station)
        {
            TargetId = hitId,
            Vector = relativePos,
            Velocity = velocity
        };
    }

    public static CrewCommand LaunchMissile(
        PersistentID aircraftId,
        sbyte station,
        PersistentID targetId,
        GlobalPosition aimpoint
    )
    {
        return new CrewCommand(CrewCommandKind.LaunchMissile, aircraftId, station)
        {
            TargetId = targetId,
            Aimpoint = aimpoint
        };
    }

    public static CrewCommand TurretVector(PersistentID aircraftId, sbyte station, Vector3Compressed direction)
    {
        return new CrewCommand(CrewCommandKind.TurretVector, aircraftId, station) { Vector = direction };
    }

    public static CrewCommand SetStationTargets(PersistentID aircraftId, sbyte station, PersistentID[] targets)
    {
        return new CrewCommand(CrewCommandKind.SetStationTargets, aircraftId, station) { Targets = targets };
    }

    public static CrewCommand SelectStation(PersistentID aircraftId, sbyte station)
    {
        return new CrewCommand(CrewCommandKind.SelectStation, aircraftId, station);
    }

    public void Read(NetworkReader reader)
    {
        Kind = (CrewCommandKind)reader.ReadByte();
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        Targets = [];

        switch (Kind)
        {
            case CrewCommandKind.FiringState:
                Firing = reader.ReadBoolean();
                break;
            case CrewCommandKind.SingleFire:
            case CrewCommandKind.StoppedFiring:
            case CrewCommandKind.SelectStation:
                break;
            case CrewCommandKind.ClaimHit:
                TargetId = new PersistentID { Id = reader.ReadUInt32() };
                Vector = reader.Read<Vector3Compressed>();
                Velocity = reader.Read<Vector3Compressed>();
                break;
            case CrewCommandKind.LaunchMissile:
                TargetId = new PersistentID { Id = reader.ReadUInt32() };
                Aimpoint = reader.ReadGlobalPosition();
                break;
            case CrewCommandKind.TurretVector:
                Vector = reader.Read<Vector3Compressed>();
                break;
            case CrewCommandKind.SetStationTargets:
                var count = reader.ReadByte();
                if (count > MaxTargets)
                {
                    throw new InvalidOperationException($"Crew target list of {count} exceeds {MaxTargets}.");
                }

                Targets = new PersistentID[count];
                for (var i = 0; i < count; i++)
                {
                    Targets[i] = new PersistentID { Id = reader.ReadUInt32() };
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown crew command {Kind}.");
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteByte((byte)Kind);
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);

        switch (Kind)
        {
            case CrewCommandKind.FiringState:
                writer.WriteBoolean(Firing);
                break;
            case CrewCommandKind.ClaimHit:
                writer.WriteUInt32(TargetId.Id);
                writer.Write(Vector);
                writer.Write(Velocity);
                break;
            case CrewCommandKind.LaunchMissile:
                writer.WriteUInt32(TargetId.Id);
                writer.WriteGlobalPosition(Aimpoint);
                break;
            case CrewCommandKind.TurretVector:
                writer.Write(Vector);
                break;
            case CrewCommandKind.SetStationTargets:
                if (Targets.Length > MaxTargets)
                {
                    throw new InvalidOperationException($"Crew target list of {Targets.Length} exceeds {MaxTargets}.");
                }

                writer.WriteByte((byte)Targets.Length);
                foreach (var target in Targets)
                {
                    writer.WriteUInt32(target.Id);
                }

                break;
        }
    }
}

internal struct CrewTurretVector : IMessage<CrewTurretVector>
{
    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public Vector3Compressed Direction { get; private set; }

    public CrewTurretVector(PersistentID aircraftId, sbyte station, Vector3Compressed direction)
    {
        AircraftId = aircraftId;
        Station = station;
        Direction = direction;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        Direction = reader.Read<Vector3Compressed>();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);
        writer.Write(Direction);
    }
}

internal struct CrewLaunch : IMessage<CrewLaunch>
{
    public PersistentID AircraftId { get; private set; }

    public sbyte Station { get; private set; }

    public PersistentID TargetId { get; private set; }

    public GlobalPosition Aimpoint { get; private set; }

    public CrewLaunch(PersistentID aircraftId, sbyte station, PersistentID targetId, GlobalPosition aimpoint)
    {
        AircraftId = aircraftId;
        Station = station;
        TargetId = targetId;
        Aimpoint = aimpoint;
    }

    public void Read(NetworkReader reader)
    {
        AircraftId = new PersistentID { Id = reader.ReadUInt32() };
        Station = reader.ReadSByte();
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        Aimpoint = reader.ReadGlobalPosition();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(AircraftId.Id);
        writer.WriteSByte(Station);
        writer.WriteUInt32(TargetId.Id);
        writer.WriteGlobalPosition(Aimpoint);
    }
}

internal struct CrewKillAuthor : IMessage<CrewKillAuthor>
{
    public PersistentID KilledId { get; private set; }

    public int PlayerIndex { get; private set; }

    public CrewKillAuthor(PersistentID killedId, int playerIndex)
    {
        KilledId = killedId;
        PlayerIndex = playerIndex;
    }

    public void Read(NetworkReader reader)
    {
        KilledId = new PersistentID { Id = reader.ReadUInt32() };
        PlayerIndex = reader.ReadPackedInt32();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(KilledId.Id);
        writer.WritePackedInt32(PlayerIndex);
    }
}

internal struct CrewHit : IMessage<CrewHit>
{
    public PersistentID TargetId { get; private set; }

    public Vector3Compressed RelativePos { get; private set; }

    public CrewHit(PersistentID targetId, Vector3Compressed relativePos)
    {
        TargetId = targetId;
        RelativePos = relativePos;
    }

    public void Read(NetworkReader reader)
    {
        TargetId = new PersistentID { Id = reader.ReadUInt32() };
        RelativePos = reader.Read<Vector3Compressed>();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteUInt32(TargetId.Id);
        writer.Write(RelativePos);
    }
}

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

internal struct CrewOpenPilots : IMessage<CrewOpenPilots>
{
    private const int MaxCount = 1024;

    public int[] PlayerIndices { get; private set; }

    public CrewOpenPilots(int[] playerIndices)
    {
        PlayerIndices = playerIndices;
    }

    public void Read(NetworkReader reader)
    {
        var count = reader.ReadPackedInt32();
        if (count < 0 || count > MaxCount)
        {
            throw new InvalidOperationException($"Open pilot list of {count} is outside 0..{MaxCount}.");
        }

        PlayerIndices = new int[count];
        for (var i = 0; i < count; i++)
        {
            PlayerIndices[i] = reader.ReadPackedInt32();
        }
    }

    public void Write(NetworkWriter writer)
    {
        writer.WritePackedInt32(PlayerIndices.Length);
        foreach (var index in PlayerIndices)
        {
            writer.WritePackedInt32(index);
        }
    }
}