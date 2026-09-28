using UnityEngine;

namespace NoMulticrew.Seats;

internal static class SeatTableFile
{
    public static string Path => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "NoMulticrew.seats.json");

    public static SeatTable? TryRead()
    {
        if (!System.IO.File.Exists(Path))
        {
            return null;
        }

        try
        {
            var document = JsonUtility.FromJson<SeatTableDocument>(System.IO.File.ReadAllText(Path));

            if (document?.airframes == null)
            {
                Plugin.Logger.LogError($"Seat table {Path} has no 'airframes' array; using defaults.");

                return null;
            }

            var byJsonKey = new Dictionary<string, IReadOnlyList<SeatDefinition>>();

            foreach (var airframe in document.airframes)
            {
                if (string.IsNullOrEmpty(airframe.jsonKey))
                {
                    Plugin.Logger.LogError($"Seat table {Path} has an airframe with no jsonKey; skipped.");

                    continue;
                }

                byJsonKey[airframe.jsonKey] = airframe.seats.Select(seat => seat.ToDefinition()).ToList();
            }

            return new SeatTable(byJsonKey);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Seat table {Path} could not be read, using defaults: {e}");

            return null;
        }
    }
}
