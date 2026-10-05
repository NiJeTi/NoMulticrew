using HarmonyLib;
using NuclearOption.UIStyleSystem;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew.Ui;

internal sealed class ScreenRow : IDisposable
{
    public const float Height = 24f;

    private const float LabelInset = 8f;

    private static readonly AccessTools.FieldRef<MapOptions_ToggleButton, TextMeshProUGUI> LabelRef =
        AccessTools.FieldRefAccess<MapOptions_ToggleButton, TextMeshProUGUI>("label");

    private static readonly AccessTools.FieldRef<MapOptions_ToggleButton, MonoBehaviour?> ScriptRef =
        AccessTools.FieldRefAccess<MapOptions_ToggleButton, MonoBehaviour?>("script");

    private static readonly AccessTools.FieldRef<MapOptions_ToggleButton, string> VariableNameRef =
        AccessTools.FieldRefAccess<MapOptions_ToggleButton, string>("variableName");

    private static readonly AccessTools.FieldRef<MapOptions_ToggleButton, List<MapOptions_ToggleButton>>
        OtherButtonsRef =
            AccessTools.FieldRefAccess<MapOptions_ToggleButton, List<MapOptions_ToggleButton>>("otherButtons");

    private readonly GameObject _root;
    private readonly MapOptions_ToggleButton? _button;
    private readonly Func<bool>? _getState;

    private ScreenRow(GameObject root, MapOptions_ToggleButton? button, Func<bool>? getState = null)
    {
        _root = root;
        _button = button;
        _getState = getState;
    }

    public static ScreenRow CreateToggle(
        Transform container,
        MapOptions_ToggleButton template,
        string label,
        Func<bool> getState,
        Action onToggle
    )
    {
        var button = Clone(container, template, $"Toggle: {label}");

        Style(LabelRef(button), label, TextAlignmentOptions.Center, LabelInset);
        Bind(button, getState(), onToggle);

        return new ScreenRow(button.gameObject, button, getState);
    }

    public static ScreenRow CreateButton(
        Transform container,
        MapOptions_ToggleButton template,
        string label,
        Action onClick
    )
    {
        return CreateToggle(container, template, label, () => true, onClick);
    }

    public static ScreenRow CreateLabel(Transform container, MapOptions_ToggleButton template, string text, bool usable = true)
    {
        var label = Object.Instantiate(LabelRef(template), container);
        label.gameObject.name = $"NoMulticrew.CrewScreen.Label: {text}";
        label.color = usable ? ThemeManager.Active.ColorTheme.AllClear : Color.grey;

        var rect = (RectTransform)label.transform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Height);

        Style(label, text, TextAlignmentOptions.Left, LabelInset);
        label.gameObject.SetActive(true);

        return new ScreenRow(label.gameObject, null);
    }

    public void Dispose()
    {
        Object.DestroyImmediate(_root);
    }

    public void UpdateState()
    {
        if (_button != null && _getState != null)
        {
            _button.Set(_getState());
        }
    }

    private static MapOptions_ToggleButton Clone(Transform container, MapOptions_ToggleButton template, string name)
    {
        var button = Object.Instantiate(template, container);
        button.gameObject.name = $"NoMulticrew.CrewScreen.{name}";

        ScriptRef(button) = null;
        VariableNameRef(button) = string.Empty;
        OtherButtonsRef(button) = [];

        return button;
    }

    private static void Style(TextMeshProUGUI text, string content, TextAlignmentOptions alignment, float inset)
    {
        text.text = content;
        text.alignment = alignment;
        text.margin = new Vector4(inset, 0f, inset, 0f);
    }

    private static void Bind(MapOptions_ToggleButton button, bool state, Action onToggle)
    {
        button.Set(state);

        button.OnToggleMethod = new MapOptions_ToggleButton.CallBackEvent();
        button.OnToggleMethod.AddListener(() => onToggle());

        button.gameObject.SetActive(true);
    }
}