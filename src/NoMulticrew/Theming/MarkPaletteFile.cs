using NuclearOption.UIStyleSystem;
using UnityEngine;

namespace NoMulticrew.Theming;

internal static class MarkPaletteFile
{
    private const string FileName = "NoMulticrew.json";
    private const string ThemesFolder = "themes";

    [Serializable]
    private sealed class Dto
    {
        public string id = string.Empty;
        public string crewTarget = string.Empty;
    }

    public static Color? Read(ThemeGroup group)
    {
        var path = PathFor(group);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var dto = JsonUtility.FromJson<Dto>(File.ReadAllText(path));
            if (dto == null || !TryParse(dto.crewTarget, out var color))
            {
                Plugin.Logger.LogWarning($"Ignoring invalid mark palette: {path}");
                return null;
            }

            return color;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to read mark palette {path}: {e}");
            return null;
        }
    }

    public static void Write(ThemeGroup group, Color crewTarget)
    {
        var path = PathFor(group);

        try
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var dto = new Dto { id = group.Id, crewTarget = ColorUtility.ToHtmlStringRGB(crewTarget) };
            File.WriteAllText(path, JsonUtility.ToJson(dto, true));

            Plugin.Logger.LogDebug($"Saved mark palette: {path}");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to write mark palette {path}: {e}");
        }
    }

    private static string PathFor(ThemeGroup group)
    {
        return Path.Combine(Application.persistentDataPath, ThemesFolder, group.name, FileName);
    }

    private static bool TryParse(string hex, out Color color)
    {
        color = default;

        return !string.IsNullOrWhiteSpace(hex)
            && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : $"#{hex}", out color);
    }
}
