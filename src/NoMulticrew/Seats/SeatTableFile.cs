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

        var document = TryReadDocument();
        if (document?.aircraft.Length is not > 0)
        {
            return null;
        }

        var config = GetValidConfig(document);
        if (config.Count == 0)
        {
            return null;
        }

        return new SeatTable(config);
    }

    private static Dictionary<string, SeatDefinition[]> GetValidConfig(Document document)
    {
        var config = new Dictionary<string, SeatDefinition[]>(document.aircraft.Length);

        for (var i = 0; i < document.aircraft.Length; i++)
        {
            var aircraft = document.aircraft[i];

            if (string.IsNullOrWhiteSpace(aircraft.name))
            {
                Plugin.Logger.LogWarning($"Seat table file: Invalid name for aircraft {i}");
                continue;
            }

            var seats = new List<SeatDefinition>(aircraft.seats.Length);
            for (var j = 0; j < aircraft.seats.Length; j++)
            {
                var seat = aircraft.seats[j];

                if (seat.role == SeatRole.None || !Enum.IsDefined(typeof(SeatRole), seat.role))
                {
                    Plugin.Logger.LogWarning(
                        $"Seat table file: Invalid role for seat {j} at aircraft '{aircraft.name}'"
                    );
                    continue;
                }

                if (seat.viewOffset == Vector3.zero)
                {
                    Plugin.Logger.LogWarning(
                        $"Seat table file: Invalid view offset for seat {j} at aircraft '{aircraft.name}'"
                    );
                    continue;
                }

                seats.Add(new SeatDefinition(seat.role, seat.viewOffset));
            }

            config[aircraft.name] = [.. seats];
        }

        return config;
    }

    private static Document? TryReadDocument()
    {
        try
        {
            return JsonUtility.FromJson<Document>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to read seat table file: {e}");

            return null;
        }
    }
}