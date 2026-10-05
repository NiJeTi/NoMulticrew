using NuclearOption.UIStyleSystem;
using UnityEngine;

namespace NoMulticrew.Theming;

internal sealed class MarkPalette
{
    public static readonly Color DefaultCrewTarget = new(1f, 0.75f, 0f);

    private readonly Dictionary<string, Color> _colors = [];

    public Color Active => Get(ThemeManager.Active);

    public void Set(string id, Color color)
    {
        _colors[id] = color;
    }

    public void Save(ThemeGroup group)
    {
        if (group.Origin == ThemeGroup.ThemeOrigin.Scriptable_Object)
        {
            return;
        }

        MarkPaletteFile.Write(group, Get(group));
    }

    public void Copy(ThemeGroup from, string toId)
    {
        _colors[toId] = Get(from);
    }

    public void Drop(string id)
    {
        _colors.Remove(id);
    }

    private Color Get(ThemeGroup group)
    {
        if (_colors.TryGetValue(group.Id, out var cached))
        {
            return cached;
        }

        var color = MarkPaletteFile.Read(group) ?? DefaultCrewTarget;
        _colors[group.Id] = color;

        return color;
    }
}
