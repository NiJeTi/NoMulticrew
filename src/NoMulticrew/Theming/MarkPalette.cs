using NuclearOption.UIStyleSystem;
using UnityEngine;

namespace NoMulticrew.Theming;

internal sealed class MarkPalette
{
    public static readonly Color DefaultCrewTarget = new(1f, 0.75f, 0f);

    private readonly Dictionary<string, Color> _colors = [];

    public Color Active => Get(ThemeManager.Active);

    private Color Get(ThemeGroup? group)
    {
        if (group == null)
        {
            return DefaultCrewTarget;
        }

        return _colors.TryGetValue(group.Id, out var color) ? color : DefaultCrewTarget;
    }
}
