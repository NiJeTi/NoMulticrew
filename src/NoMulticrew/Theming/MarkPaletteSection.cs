using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Theming;

internal sealed class MarkPaletteSection : IDisposable
{
    private const string Title = "NoMulticrew Marks";
    private const string CrewTargetLabel = "Crew target";
    private const string CrewTargetTooltip =
        "Color of targets selected by the other seats of your aircraft, on the HUD and the map, and of the lines drawn to them.";
    private const string NoWingmenSectionName = "NoWingmen.PaletteSection";

    private static readonly AccessTools.FieldRef<AccessibilityMenu, ColorPicker> ColorPickerPrefabRef =
        AccessTools.FieldRefAccess<AccessibilityMenu, ColorPicker>("colorPickerPrefab");

    private static readonly AccessTools.FieldRef<AccessibilityMenu, CategoryTitle> CategoryTitlePrefabRef =
        AccessTools.FieldRefAccess<AccessibilityMenu, CategoryTitle>("categoryTitlePrefab");

    private static readonly AccessTools.FieldRef<AccessibilityMenu, Transform> MenuThemeTransformRef =
        AccessTools.FieldRefAccess<AccessibilityMenu, Transform>("menuThemeTransform");

    private static readonly AccessTools.FieldRef<AccessibilityMenu, SliderToggle> SliderToggleRef =
        AccessTools.FieldRefAccess<AccessibilityMenu, SliderToggle>("themeGroupSliderToggle");

    private static readonly AccessTools.FieldRef<AccessibilityMenu, bool> IsThemeEditedRef =
        AccessTools.FieldRefAccess<AccessibilityMenu, bool>("isThemeEdited");

    private static MarkPaletteSection? _current;

    private readonly AccessibilityMenu _menu;
    private readonly GameObject _container;
    private readonly ColorPicker _picker;

    private readonly Action _refreshThemeName;
    private readonly Action _refreshButtons;
    private readonly Action<string> _hoverIn;
    private readonly Action<string> _hoverOut;

    private MarkPaletteSection(AccessibilityMenu menu, GameObject container, ColorPicker picker)
    {
        _menu = menu;
        _container = container;
        _picker = picker;

        _refreshThemeName = AccessTools.MethodDelegate<Action>(
            AccessTools.Method(typeof(AccessibilityMenu), "RefreshThemeName"), menu
        );
        _refreshButtons = AccessTools.MethodDelegate<Action>(
            AccessTools.Method(typeof(AccessibilityMenu), "RefreshButtons"), menu
        );
        _hoverIn = AccessTools.MethodDelegate<Action<string>>(
            AccessTools.Method(typeof(AccessibilityMenu), "OnColorNameHoverIn"), menu
        );
        _hoverOut = AccessTools.MethodDelegate<Action<string>>(
            AccessTools.Method(typeof(AccessibilityMenu), "OnColorNameHoveredOut"), menu
        );

        _picker.OnColorChanged += OnColorChanged;
        _picker.OnColorNameHoverIn += _hoverIn;
        _picker.OnColorNameHoverOut += _hoverOut;
    }

    public static void Build(AccessibilityMenu menu)
    {
        _current?.Dispose();
        _current = Create(menu);
        _current.Show();
    }

    public static void Refresh(AccessibilityMenu menu)
    {
        if (_current != null && _current._menu == menu)
        {
            _current.Show();
        }
    }

    public static void Commit()
    {
        var group = ThemeManager.Active;
        if (_current == null || group == null)
        {
            return;
        }

        Plugin.Palette.Set(group.Id, _current._picker.Color.WithAlpha(1f));
        Plugin.Client?.Marks.Repaint();
    }

    public void Dispose()
    {
        _picker.OnColorChanged -= OnColorChanged;
        _picker.OnColorNameHoverIn -= _hoverIn;
        _picker.OnColorNameHoverOut -= _hoverOut;

        if (_container != null)
        {
            Object.Destroy(_container);
        }
    }

    private static MarkPaletteSection Create(AccessibilityMenu menu)
    {
        var template = MenuThemeTransformRef(menu);

        var wasActive = template.gameObject.activeSelf;
        template.gameObject.SetActive(false);

        var container = Object.Instantiate(template.gameObject, template.parent);
        template.gameObject.SetActive(wasActive);

        container.name = "NoMulticrew.MarkPaletteSection";

        var noWingmen = template.parent.Find(NoWingmenSectionName);
        var after = noWingmen != null ? noWingmen : template;
        container.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);

        for (var i = container.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(container.transform.GetChild(i).gameObject);
        }

        container.SetActive(true);

        var title = Object.Instantiate(CategoryTitlePrefabRef(menu), container.transform);
        title.name = "NoMulticrew.MarkPaletteSection.Title";
        title.Text = Title;
        title.transform.SetSiblingIndex(0);

        var picker = Object.Instantiate(ColorPickerPrefabRef(menu), container.transform);

        Plugin.Logger.LogInfo("Mark palette section created");

        return new MarkPaletteSection(menu, container, picker);
    }

    private void Show()
    {
        _picker.SetValues(CrewTargetLabel, Plugin.Palette.Active, CrewTargetTooltip);

        var toggle = SliderToggleRef(_menu);
        _container.SetActive(toggle != null && toggle.isOn);

        if (_container.transform.parent is RectTransform parent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }

    private void OnColorChanged(Color color)
    {
        IsThemeEditedRef(_menu) = true;
        _refreshThemeName();
        _refreshButtons();
    }
}
