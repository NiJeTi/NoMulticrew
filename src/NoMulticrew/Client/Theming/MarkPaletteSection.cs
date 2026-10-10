using System.Reflection;
using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Client.Theming;

internal sealed class MarkPaletteSection : IDisposable
{
    private const string Title = "NoMulticrew Marks";
    private const string CrewTargetLabel = "Crew target";

    private const string CrewTargetTooltip = "Color of targets selected by the other seats of your aircraft.";

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

    private static readonly MethodInfo RefreshThemeName = GameMembers.Method(
        typeof(AccessibilityMenu), "RefreshThemeName"
    );

    private static readonly MethodInfo RefreshButtons = GameMembers.Method(typeof(AccessibilityMenu), "RefreshButtons");

    private static readonly MethodInfo HoverIn = GameMembers.Method(typeof(AccessibilityMenu), "OnColorNameHoverIn");

    private static readonly MethodInfo HoverOut = GameMembers.Method(
        typeof(AccessibilityMenu), "OnColorNameHoveredOut"
    );

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

        _refreshThemeName = AccessTools.MethodDelegate<Action>(RefreshThemeName, menu);
        _refreshButtons = AccessTools.MethodDelegate<Action>(RefreshButtons, menu);
        _hoverIn = AccessTools.MethodDelegate<Action<string>>(HoverIn, menu);
        _hoverOut = AccessTools.MethodDelegate<Action<string>>(HoverOut, menu);

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
        if (_current == null)
        {
            return;
        }

        Plugin.Palette.Set(ThemeManager.Active.Id, _current._picker.Color.WithAlpha(1f));
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

        _container.SetActive(SliderToggleRef(_menu).isOn);

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