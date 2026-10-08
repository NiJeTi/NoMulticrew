using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Client.Ui;

internal sealed class CrewScreenLayout : IDisposable
{
    private const string ShortName = "CRW";

    private const string ScreenTitle = "CREW";

    private const string CrewTitle = "CREW";
    private const string StationsTitle = "STATIONS";
    private const string ActionsTitle = "ACTIONS";
    private const string SeatsTitle = "SEATS";

    private static readonly AccessTools.FieldRef<VirtualMFD, List<Button>> LeftButtonsRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<Button>>("leftButtons");

    private static readonly AccessTools.FieldRef<VirtualMFD, List<MFDScreen?>> LeftScreensRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<MFDScreen?>>("leftScreens");

    private int _slot = -1;

    private CrewScreenLayout(
        VirtualMFD mfd,
        MFDScreen screen,
        ScreenSection crew,
        ScreenSection stations,
        ScreenSection actions,
        ScreenSection seats,
        MapOptions_ToggleButton rowTemplate
    )
    {
        Mfd = mfd;
        Screen = screen;
        Crew = crew;
        Stations = stations;
        Actions = actions;
        Seats = seats;
        RowTemplate = rowTemplate;
    }

    public VirtualMFD Mfd { get; }
    public MFDScreen Screen { get; }

    public ScreenSection Crew { get; }
    public ScreenSection Stations { get; }
    public ScreenSection Actions { get; }
    public ScreenSection Seats { get; }

    public MapOptions_ToggleButton RowTemplate { get; }

    public static CrewScreenLayout? Create(VirtualMFD mfd)
    {
        var mapOptions = SceneSingleton<MapOptions>.i;
        if (mapOptions == null)
        {
            throw new InvalidOperationException($"{nameof(MapOptions)} is null.");
        }

        var mapScreen = mapOptions.screen;

        var holder = new GameObject("NoMulticrew.CrewScreen.Holder");
        holder.transform.SetParent(mapScreen.transform.parent, false);
        holder.SetActive(false);

        var screen = Object.Instantiate(mapScreen, holder.transform);
        screen.name = "NoMulticrew.CrewScreen";

        Object.DestroyImmediate(screen.GetComponentInChildren<MapOptions>(true));

        screen.transform.SetParent(mapScreen.transform.parent, false);
        holder.transform.SetParent(screen.transform, false);

        screen.aircraftOnly = false;

        var panel = screen.displayPanel.transform;

        var rowTemplate = panel.GetComponentInChildren<MapOptions_ToggleButton>(true);
        var rowContainer = (RectTransform)rowTemplate.transform.parent;
        var sectionRoot = rowContainer.parent;

        var panelHeading = ScreenSection.FindHeading(panel);
        var sectionHeading = ScreenSection.FindHeading(sectionRoot);

        var templateRect = (RectTransform)rowTemplate.transform;
        templateRect.SetParent(holder.transform, false);
        rowTemplate.gameObject.SetActive(false);
        templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x, ScreenRow.Height);

        StripPanel(panel, rowContainer, panelHeading.transform, sectionHeading.transform);

        panelHeading.text = ScreenTitle;
        ScreenSection.StackFromTop(sectionRoot.parent);

        var crew = ScreenSection.Claim(rowContainer, sectionHeading, CrewTitle);
        var stations = crew.Clone(StationsTitle);
        var actions = crew.Clone(ActionsTitle);
        var seats = crew.Clone(SeatsTitle);

        var layout = new CrewScreenLayout(mfd, screen, crew, stations, actions, seats, rowTemplate);

        if (!layout.TryClaimScreenSlot())
        {
            Object.Destroy(screen.gameObject);

            return null;
        }

        screen.gameObject.SetActive(true);
        mfd.HideAllLeftScreens();

        return layout;
    }

    public void Dispose()
    {
        if (Mfd == null)
        {
            return;
        }

        var screens = LeftScreensRef(Mfd);
        if (ReferenceEquals(screens[_slot], Screen))
        {
            screens[_slot] = null;
        }

        LeftButtonsRef(Mfd)[_slot].onClick = new Button.ButtonClickedEvent();

        Mfd.SetupButtons();

        Object.Destroy(Screen.gameObject);
    }

    private bool TryClaimScreenSlot()
    {
        var buttons = LeftButtonsRef(Mfd);
        var screens = LeftScreensRef(Mfd);

        var index = FindFreeSlotIndex(buttons, screens);
        if (index < 0)
        {
            Plugin.Logger.LogError("No free MFD slot for the crew screen");

            return false;
        }

        var button = buttons[index];

        if (!TryBindSlotVisuals(Screen, button))
        {
            Plugin.Logger.LogError($"MFD button {index} has no label or highlight to bind");

            return false;
        }

        if (index == screens.Count)
        {
            screens.Add(Screen);
        }
        else
        {
            screens[index] = Screen;
        }

        _slot = index;

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => Mfd.PressLeftButton(button));
        button.enabled = true;

        Screen.Setup(Mfd, ShortName);
        Mfd.SetupButtons();

        return true;
    }

    private static void StripPanel(Transform panel, params Transform[] keepLeaves)
    {
        var keep = new HashSet<Transform> { panel };

        foreach (var leaf in keepLeaves)
        {
            for (var t = leaf; t != null && t != panel; t = t.parent)
            {
                keep.Add(t);
            }
        }

        DestroyChildrenExcept(panel, keep);
    }

    private static void DestroyChildrenExcept(Transform parent, HashSet<Transform> keep)
    {
        var children = parent.Cast<Transform>().ToList();

        foreach (var child in children)
        {
            if (keep.Contains(child))
            {
                DestroyChildrenExcept(child, keep);
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static bool TryBindSlotVisuals(MFDScreen screen, Button button)
    {
        var label = FindByName<TextMeshProUGUI>(button.transform, screen.label);
        var highlight = FindByName<Image>(button.transform, screen.highlight);

        if (label == null || highlight == null)
        {
            return false;
        }

        screen.label = label;
        screen.highlight = highlight;

        return true;
    }

    private static T? FindByName<T>(Transform parent, Component? source) where T : Component
    {
        if (source == null)
        {
            return null;
        }

        var child = parent.Find(source.name);

        return child != null ? child.GetComponent<T>() : null;
    }

    private static int FindFreeSlotIndex(List<Button> buttons, List<MFDScreen?> screens)
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            if (i >= screens.Count || screens[i] == null)
            {
                return i;
            }
        }

        return -1;
    }
}
