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
        ScreenSection seats,
        MapOptions_ToggleButton rowTemplate
    )
    {
        public VirtualMFD Mfd { get; } = mfd;
        public MFDScreen Screen { get; } = screen;

        public ScreenSection Crew { get; } = crew;
        public ScreenSection Stations { get; } = stations;
        public ScreenSection Actions { get; } = actions;
        public ScreenSection Seats { get; } = seats;

        public MapOptions_ToggleButton RowTemplate { get; } = rowTemplate;
    }

    private enum OfferState
    {
        Request,
        Waiting,
        Blocked,
    }

    private readonly record struct SeatOffer(
        string Airbase,
        string Text,
        PersistentID AircraftId,
        byte SeatIndex,
        OfferState State
    );

    private sealed class Content(
        string[] crew,
        (string Text, bool Usable)[] stations,
        string? request,
        bool isPilot,
        bool isBackSeat,
        bool acceptingRequests,
        bool leaveArmed,
        bool boarding,
        SeatOffer[] seats
    )
    {
        public string[] Crew { get; } = crew;
        public (string Text, bool Usable)[] Stations { get; } = stations;
        public string? Request { get; } = request;
        public bool IsPilot { get; } = isPilot;
        public bool IsBackSeat { get; } = isBackSeat;
        public bool AcceptingRequests { get; } = acceptingRequests;
        public bool LeaveArmed { get; } = leaveArmed;
        public bool Boarding { get; } = boarding;
        public SeatOffer[] Seats { get; } = seats;

        public bool Same(Content? other)
        {
            return other != null
                && Crew.SequenceEqual(other.Crew)
                && Stations.SequenceEqual(other.Stations)
                && Request == other.Request
                && IsPilot == other.IsPilot
                && IsBackSeat == other.IsBackSeat
                && AcceptingRequests == other.AcceptingRequests
                && LeaveArmed == other.LeaveArmed
                && Boarding == other.Boarding
                && Seats.SequenceEqual(other.Seats);
        }
    }

    private const string ShortName = "CRW";

    private const string ScreenTitle = "CREW";

    private const string CrewTitle = "CREW";
    private const string StationsTitle = "STATIONS";
    private const string ActionsTitle = "ACTIONS";
    private const string SeatsTitle = "SEATS";

    private const float OffersIntervalSeconds = 0.5f;

    private static readonly AccessTools.FieldRef<VirtualMFD, List<Button>> LeftButtonsRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<Button>>("leftButtons");

    private static readonly AccessTools.FieldRef<VirtualMFD, List<MFDScreen?>> LeftScreensRef =
        AccessTools.FieldRefAccess<VirtualMFD, List<MFDScreen?>>("leftScreens");

    private readonly ClientSession _session;

    private readonly Layout _layout;
    private readonly int _screenIndex;

    private readonly List<ScreenRow> _rows = [];

    private SeatOffer[] _offers = [];
    private float _offersAt = float.NegativeInfinity;

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
        var stations = new List<(string, bool)>();

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
                DescribeSeats(aircraft, state, crew);
            }
            else
            {
                crew.Add("NO CREW");
            }

            DescribeStations(aircraft, stations);
        }

        if (stations.Count == 0)
        {
            stations.Add(("NONE", true));
        }

        var request = _session.Prompt.Pending is { } prompt
            ? $"{CrewJoinPromptUi.NameOf(prompt.JoinerPlayerIndex)} WANTS {CrewJoinPromptUi.SeatLabel(prompt).ToUpperInvariant()}"
            : null;

        var boarding = backSeat == null && aircraft == null;

        return new Content(
            [.. crew],
            [.. stations],
            request,
            isPilot: aircraft != null && backSeat == null,
            isBackSeat: backSeat != null,
            acceptingRequests: !Plugin.Settings.RejectAllRequests.Value,
            leaveArmed: _session.BackSeat.BailOutArmed,
            boarding: boarding,
            seats: boarding ? Offers() : []
        );
    }

    private SeatOffer[] Offers()
    {
        var now = Time.timeSinceLevelLoad;
        if (now - _offersAt < OffersIntervalSeconds)
        {
            return _offers;
        }

        _offersAt = now;

        var offers = new List<SeatOffer>();

        if (GameManager.GetLocalPlayer<Player>(out var local))
        {
            foreach (var aircraft in Object.FindObjectsOfType<Aircraft>())
            {
                var key = aircraft.definition.jsonKey;
                var seats = Plugin.SeatTable.SeatsFor(key);

                if (seats.Count == 0
                    || !JoinRequests.CanBoard(aircraft, local, out _)
                    || !JoinRequests.TryGetBoardingAirbase(aircraft, out var airbase))
                {
                    continue;
                }

                var name = airbase.SavedAirbase.DisplayName;
                var airbaseName = (string.IsNullOrEmpty(name) ? airbase.name : name).ToUpperInvariant();
                var pilot = aircraft.Player.GetDisplayName(PlayerNameContext.Other);

                for (var i = 0; i < seats.Count; i++)
                {
                    if (_session.Crew.IsTaken(aircraft.persistentID, i) || !CrewState.OwnsAny(aircraft, i))
                    {
                        continue;
                    }

                    var state = _session.IsRequested(aircraft.persistentID, (byte)i)
                        ? OfferState.Waiting
                        : _session.HasRequest ? OfferState.Blocked : OfferState.Request;

                    offers.Add(
                        new SeatOffer(
                            airbaseName,
                            $"{aircraft.definition.unitName}  ·  {pilot}  {Plugin.SeatTable.Label(key, i).ToUpperInvariant()}",
                            aircraft.persistentID,
                            (byte)i,
                            state
                        )
                    );
                }
            }
        }

        _offers = [.. offers.OrderBy(x => x.Airbase).ThenBy(x => x.Text)];

        return _offers;
    }

    private void DescribeSeats(Aircraft aircraft, CrewRoster state, List<string> crew)
    {
        for (var seat = 0; seat < state.Occupants.Length; seat++)
        {
            var occupant = state.Occupants[seat];
            var name = occupant < 0 ? "EMPTY" : CrewJoinPromptUi.NameOf(occupant);
            var here = ReferenceEquals(_session.BackSeat.Aircraft, aircraft) && seat == _session.BackSeat.SeatIndex
                ? " <"
                : "";

            crew.Add($"{Plugin.SeatTable.Label(aircraft.definition.jsonKey, seat).ToUpperInvariant()}  {name}{here}");
        }

        var mine = ReferenceEquals(_session.BackSeat.Aircraft, aircraft) ? _session.BackSeat.SeatIndex : -1;
        if (mine >= 0 && mine < state.Pending.Length)
        {
            crew.Add($"PENDING  +{state.Pending[mine]:F0}");
        }
    }

    private void DescribeStations(Aircraft aircraft, List<(string, bool)> stations)
    {
        var key = aircraft.definition.jsonKey;
        var mine = ReferenceEquals(_session.BackSeat.Aircraft, aircraft) ? _session.BackSeat.SeatIndex : -1;
        var selected = mine >= 0
            ? _session.BackSeat.Station
            : aircraft.weaponManager.currentWeaponStation?.Number ?? -1;

        for (var i = 0; i < aircraft.weaponStations.Count; i++)
        {
            var weapon = aircraft.weaponStations[i].WeaponInfo;
            var owner = _session.Crew.OwnerSeat(aircraft, i);
            var label = owner < 0 ? "PILOT" : Plugin.SeatTable.Label(key, owner).ToUpperInvariant();
            var usable = mine >= 0 ? owner == mine : owner < 0;

            stations.Add(
                ($"{i}  {(weapon != null ? weapon.shortName : "-")}  {label}{(i == selected ? " >" : "")}", usable)
            );
        }
    }

    private void Rebuild(Content content)
    {
        DisposeRows();

        _layout.Crew.SetVisible(!content.Boarding);
        _layout.Stations.SetVisible(!content.Boarding);
        _layout.Actions.SetVisible(!content.Boarding);
        _layout.Seats.SetVisible(content.Boarding);

        if (content.Boarding)
        {
            RebuildSeats(content);
            _layout.Seats.Fit();
            return;
        }

        foreach (var line in content.Crew)
        {
            _rows.Add(ScreenRow.CreateLabel(_layout.Crew.Container, _layout.RowTemplate, line));
        }

        foreach (var (text, usable) in content.Stations)
        {
            _rows.Add(ScreenRow.CreateLabel(_layout.Stations.Container, _layout.RowTemplate, text, usable));
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
                ScreenRow.CreateButton(
                    actions,
                    template,
                    content.LeaveArmed ? "CONFIRM BAIL OUT" : "LEAVE",
                    _session.BackSeat.RequestLeave
                )
            );
        }

        _layout.Crew.Fit();
        _layout.Stations.Fit();
        _layout.Actions.Fit();
    }

    private void RebuildSeats(Content content)
    {
        var container = _layout.Seats.Container;
        var template = _layout.RowTemplate;

        if (content.Seats.Length == 0)
        {
            _rows.Add(ScreenRow.CreateLabel(container, template, "NO SEATS AVAILABLE"));
            return;
        }

        foreach (var group in content.Seats.GroupBy(x => x.Airbase))
        {
            _rows.Add(ScreenRow.CreateLabel(container, template, group.Key));

            foreach (var offer in group)
            {
                var row = _layout.Seats.AddRow();

                _rows.Add(ScreenRow.CreateLabel(row, template, offer.Text));
                _rows.Add(
                    ScreenRow.CreateToggle(
                        row,
                        template,
                        offer.State == OfferState.Waiting ? "WAITING" : "REQUEST",
                        () => offer.State == OfferState.Request,
                        () => _session.RequestSeat(offer.AircraftId, offer.SeatIndex)
                    )
                );
            }
        }
    }

    private void DisposeRows()
    {
        foreach (var row in _rows)
        {
            row.Dispose();
        }

        _rows.Clear();

        foreach (var container in new[] { _layout.Actions.Container, _layout.Seats.Container })
        {
            foreach (var line in container.Cast<Transform>().ToList())
            {
                Object.DestroyImmediate(line.gameObject);
            }
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
        var seats = crew.Clone(SeatsTitle);

        return new Layout(mfd, screen, crew, stations, actions, seats, rowTemplate);
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