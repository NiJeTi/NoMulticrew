using BepInEx;
using UnityEngine;

namespace NoMulticrew.Seats;

internal static class SeatTableFile
{
    [Serializable]
    private sealed class Seat
    {
        public required SeatRole role;
        public required Vector3 viewOffset;
    }

    [Serializable]
    private sealed class Aircraft
    {
        public required string name;
        public required Seat[] seats;
    }

    [Serializable]
    private sealed class Document
    {
        public required Aircraft[] aircraft;
    }

    public static string FilePath => Path.Combine(Paths.PluginPath, "Seats.json");

    public static SeatTable? TryRead()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            var document = JsonUtility.FromJson<Document>(File.ReadAllText(FilePath));

            if (document?.aircraft.Length is not > 0)
            {
                return null;
            }

            var config = document.aircraft.ToDictionary(
                a => a.name,
                a => a.seats.Select(s => new SeatDefinition(s.role, s.viewOffset)).ToArray()
            );

            return new SeatTable(config);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to read seat table file: {e}");

            return null;
        }
    }
}