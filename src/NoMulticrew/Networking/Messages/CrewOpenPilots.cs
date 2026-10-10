using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

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