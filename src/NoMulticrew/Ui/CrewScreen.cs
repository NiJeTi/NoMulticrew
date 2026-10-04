using HarmonyLib;
using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Ui;

internal sealed class CrewScreen : IDisposable
{
    private sealed class Layout(
        VirtualMFD mfd,
        MFDScreen screen,
        ScreenSection crew,
        ScreenSection stations,
        ScreenSection actions,
        MapOptions_ToggleButton rowTemplate
    )
    {
        public VirtualMFD Mfd { get; } = mfd;
        public MFDScreen Screen { get; } = screen;

        public ScreenSection Crew { get; } = crew;
        public ScreenSection Stations { get; } = stations;
        public ScreenSection Actions { get; } = actions;

        public MapOptions_ToggleButton RowTemplate { get; } = rowTemplate;
    }

    private sealed class Content(
        string[] crew,
        string[] stations,
        string? request,
        bool isPilot,
        bool isBackSeat,
        bool acceptingRequests
    )
    {
        public string[] Crew { get; } = crew;
        public string[] Stations { get; } = stations;
        public string? Request { get; } = request;
        public bool IsPilot { get; } = isPilot;
        public bool IsBackSeat { get; } = isBackSeat;
        public bool AcceptingRequests { get; } = acceptingRequests;

        public bool Same(Content? other)
        {
            return other != null
                && Crew.SequenceEqual(other.Crew)
                && Stations.SequenceEqual(other.Stations)
                && Request == other.Request
                && IsPilot == other.IsPilot
                && IsBackSeat == other.IsBackSeat
                && AcceptingRequests == other.AcceptingRequests;
        }
    }

    private const string ShortName = "CRW";

    private const string ScreenTitle = "CREW";

    private const string CrewTitle = "CREW";
    private const string StationsTitle = "STATIONS";
    private const string ActionsTitle = "ACTIONS";

    private static readonly AccessTools.FieldRef<VirtualMFD, List<Button>> LeftButtonsRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<Button>>("leftButtons");

    private static readonly AccessTools.FieldRef<VirtualMFD, List<MFDScreen?>> LeftScreensRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<MFDScreen?>>("leftScreens");

    private readonly ClientSession _session;

    private readonly Layout _layout;
    private readonly int _screenIndex;

    private readonly List<ScreenRow> _rows = [];

    private Content? _shown;

    private CrewScreen(ClientSession session, Layout layout, int screenIndex)
    {
        _session = session;
        _layout = layout;
        _screenIndex = screenIndex;
    }

    public static CrewScreen? Create(ClientSession session, VirtualMFD mfd)
    {
        var layout = BuildLayout(mfd);

        if (!TryClaimScreenSlot(layout, out var slotIndex))
        {
            Object.Destroy(layout.Screen.gameObject);

            return null;
        }

        var screen = new CrewScreen(session, layout, slotIndex);

        layout.Screen.gameObject.SetActive(true);
        mfd.HideAllLeftScreens();

        return screen;
    }

    public void Dispose()
    {
        if (_layout.Mfd == null)
        {
            _rows.Clear();
            return;
        }

        DisposeRows();
        ReleaseScreenSlot();
        Object.Destroy(_layout.Screen.gameObject);
    }

    public void Tick()
    {
        if (_layout.Mfd == null || !DynamicMap.mapMaximized)
        {
            return;
        }

        var content = Describe();

        if (!content.Same(_shown))
        {
            _shown = content;
            Rebuild(content);
        }

        foreach (var row in _rows)
        {
            row.UpdateState();
        }
    }

    private Content Describe()
    {
        var backSeat = _session.BackSeat.Aircraft;
        var aircraft = backSeat != null ? backSeat : LocalAircraft();

        var crew = new List<string>();
        var stations = new List<string>();

        if (aircraft == null)
        {
            crew.Add("NOT IN AN AIRCRAFT");
        }
        else
        {
            var pilot = aircraft.Player != null ? aircraft.Player.GetDisplayName(PlayerNameContext.Other) : "NONE";
            crew.Add($"PILOT  {pilot}");

            if (_session.Crew.TryGetCrew(aircraft.persistentID, out var state))
            {
                DescribeSeats(aircraft, state, crew, stations);
            }
            else
            {
                crew.Add("NO CREW");
            }
        }

        if (stations.Count == 0)
        {
            stations.Add("NONE");
        }

        var request = _session.Prompt.Pending is { } prompt
            ? $"{CrewJoinPromptUi.NameOf(prompt.JoinerPlayerIndex)} WANTS SEAT {prompt.SeatIndex}"
            : null;

        return new Content(
            [.. crew],
            [.. stations],
            request,
            isPilot: aircraft != null && backSeat == null,
            isBackSeat: backSeat != null,
            acceptingRequests: !Plugin.Settings.RejectAllRequests.Value
        );
    }

    private void DescribeSeats(Aircraft aircraft, MulticrewState state, List<string> crew, List<string> stations)
    {
        for (var seat = 0; seat < state.Occupants.Length; seat++)
        {
            var occupant = state.Occupants[seat];
            var name = occupant < 0 ? "EMPTY" : CrewJoinPromptUi.NameOf(occupant);
            var here = seat == _session.BackSeat.SeatIndex ? " <" : "";

            crew.Add($"{seat}  {state.Roles[seat].ToString().ToUpperInvariant()}  {name}{here}");

            if (occupant < 0)
            {
                continue;
            }

            for (var station = 0; station < aircraft.weaponStations.Count; station++)
            {
                if (!CrewState.Owns(state.Roles[seat], aircraft, station))
                {
                    continue;
                }

                var weapon = aircraft.weaponStations[station].WeaponInfo;
                stations.Add($"{station}  {(weapon != null ? weapon.shortName : "-")}  SEAT {seat}");
            }
        }
    }

    private void Rebuild(Content content)
    {
        DisposeRows();

        foreach (var line in content.Crew)
        {
            _rows.Add(ScreenRow.CreateLabel(_layout.Crew.Container, _layout.RowTemplate, line));
        }

        foreach (var line in content.Stations)
        {
            _rows.Add(ScreenRow.CreateLabel(_layout.Stations.Container, _layout.RowTemplate, line));
        }

        var actions = _layout.Actions.Container;
        var template = _layout.RowTemplate;

        if (content.IsPilot)
        {
            if (content.Request != null)
            {
                _rows.Add(ScreenRow.CreateLabel(actions, template, content.Request));

                var answer = _layout.Actions.AddRow();
                _rows.Add(ScreenRow.CreateButton(answer, template, "ACCEPT", _session.Prompt.Accept));
                _rows.Add(ScreenRow.CreateButton(answer, template, "DECLINE", _session.Prompt.Decline));
            }

            _rows.Add(
                ScreenRow.CreateToggle(
                    actions,
                    template,
                    "REQUESTS",
                    () => !Plugin.Settings.RejectAllRequests.Value,
                    () => Plugin.Settings.RejectAllRequests.Value = !Plugin.Settings.RejectAllRequests.Value
                )
            );
        }

        if (content.IsBackSeat)
        {
            _rows.Add(
                ScreenRow.CreateButton(actions, template, "LEAVE", () => _session.Send(new MulticrewLeaveRequest()))
            );
        }

        _layout.Crew.Fit();
        _layout.Stations.Fit();
        _layout.Actions.Fit();
    }

    private void DisposeRows()
    {
        foreach (var row in _rows)
        {
            row.Dispose();
        }

        _rows.Clear();

        foreach (var line in _layout.Actions.Container.Cast<Transform>().ToList())
        {
            Object.DestroyImmediate(line.gameObject);
        }
    }

    private static Aircraft? LocalAircraft()
    {
        return GameManager.GetLocalAircraft(out var aircraft) ? aircraft : null;
    }

    private void ReleaseScreenSlot()
    {
        if (_screenIndex < 0)
        {
            return;
        }

        var screens = LeftScreensRef(_layout.Mfd);
        if (_screenIndex < screens.Count && ReferenceEquals(screens[_screenIndex], _layout.Screen))
        {
            screens[_screenIndex] = null;
        }

        var buttons = LeftButtonsRef(_layout.Mfd);
        if (_screenIndex < buttons.Count)
        {
            buttons[_screenIndex].onClick = new Button.ButtonClickedEvent();
        }

        _layout.Mfd.SetupButtons();
    }

    private static Layout BuildLayout(VirtualMFD mfd)
    {
        var mapOptions = SceneSingleton<MapOptions>.i ??
            throw new InvalidOperationException($"{nameof(MapOptions)} is null.");

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

        TakeRowTemplate(rowTemplate, holder.transform);
        StripPanel(panel, rowContainer, panelHeading.transform, sectionHeading.transform);

        panelHeading.text = ScreenTitle;
        ScreenSection.StackFromTop(sectionRoot.parent);

        var crew = ScreenSection.Claim(rowContainer, sectionHeading, CrewTitle);
        var stations = crew.Clone(StationsTitle);
        var actions = crew.Clone(ActionsTitle);

        return new Layout(mfd, screen, crew, stations, actions, rowTemplate);
    }

    private static void TakeRowTemplate(MapOptions_ToggleButton template, Transform holder)
    {
        var rect = (RectTransform)template.transform;

        template.transform.SetParent(holder, false);
        template.gameObject.SetActive(false);

        rect.sizeDelta = new Vector2(rect.sizeDelta.x, ScreenRow.Height);
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

    private static bool TryClaimScreenSlot(Layout layout, out int index)
    {
        var buttons = LeftButtonsRef(layout.Mfd);
        var screens = LeftScreensRef(layout.Mfd);

        index = FindFreeSlotIndex(buttons, screens);
        if (index < 0)
        {
            Plugin.Logger.LogError("No free MFD slot for the crew screen");

            return false;
        }

        var button = buttons[index];

        if (!TryBindSlotVisuals(layout.Screen, button))
        {
            Plugin.Logger.LogError($"MFD button {index} has no label or highlight to bind");
            index = -1;

            return false;
        }

        screens[index] = layout.Screen;

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => layout.Mfd.PressLeftButton(button));
        button.enabled = true;

        layout.Screen.Setup(layout.Mfd, ShortName);
        layout.Mfd.SetupButtons();

        return true;
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
        return source == null ? null : parent.Find(source.name)?.GetComponent<T>();
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