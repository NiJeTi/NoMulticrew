using NoMulticrew.Seats;
using NuclearOption.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew.Client.Ui;

internal sealed class CrewScreen : IDisposable
{
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

    private const float OffersIntervalSeconds = 0.5f;
    private const float DescribeIntervalSeconds = 0.1f;

    private readonly ClientSession _session;

    private readonly CrewScreenLayout _layout;

    private readonly List<ScreenRow> _rows = [];

    private SeatOffer[] _offers = [];
    private float _offersAt = float.NegativeInfinity;

    private float _describedAt = float.NegativeInfinity;
    private Content? _shown;

    private CrewScreen(ClientSession session, CrewScreenLayout layout)
    {
        _session = session;
        _layout = layout;
    }

    public static CrewScreen? Create(ClientSession session, VirtualMFD mfd)
    {
        return CrewScreenLayout.Create(mfd) is { } layout ? new CrewScreen(session, layout) : null;
    }

    public void Dispose()
    {
        if (_layout.Mfd != null)
        {
            DisposeRows();
        }
        else
        {
            _rows.Clear();
        }

        _layout.Dispose();
    }

    public void Tick()
    {
        if (_layout.Mfd == null || !DynamicMap.mapMaximized)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now - _describedAt >= DescribeIntervalSeconds)
        {
            _describedAt = now;

            var content = Describe();

            if (!content.Same(_shown))
            {
                _shown = content;
                Rebuild(content);
            }
        }

        foreach (var row in _rows)
        {
            row.UpdateState();
        }
    }

    private Content Describe()
    {
        var backSeat = _session.BackSeat.Aircraft;
        var aircraft = backSeat != null ? backSeat : GameManager.GetLocalAircraft(out var local) ? local : null;

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

            if (_session.Crew.TryGetRoster(aircraft.persistentID, out var roster))
            {
                var here = backSeat != null;

                crew.Add(
                    $"{SeatTable.Label(Role.Wso).ToUpperInvariant()}  "
                    + $"{CrewState.NameOf(roster.WsoPlayerIndex)}{(here ? " <" : "")}"
                );

                if (here)
                {
                    crew.Add($"PENDING  +{roster.Pending:F0}");
                }
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

        var request = _session.Notices.Pending is { } prompt
            ? $"{CrewState.NameOf(prompt.JoinerPlayerIndex)} WANTS {SeatTable.Label(Role.Wso).ToUpperInvariant()}"
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
        var now = Time.unscaledTime;
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
                if (Plugin.SeatTable.WsoSeat(aircraft) == null
                    || !SeatTable.CanBoard(aircraft, local, out var airbase, out _)
                    || !_session.TakesCrew(aircraft.Player)
                    || _session.Crew.HasRoster(aircraft.persistentID)
                    || !Plugin.SeatTable.Offered(aircraft))
                {
                    continue;
                }

                var name = airbase.SavedAirbase.DisplayName;
                var airbaseName = (string.IsNullOrEmpty(name) ? airbase.name : name).ToUpperInvariant();
                var pilot = aircraft.Player.GetDisplayName(PlayerNameContext.Other);

                var state = _session.IsRequested(aircraft.persistentID)
                    ? OfferState.Waiting
                    : _session.HasRequest ? OfferState.Blocked : OfferState.Request;

                offers.Add(
                    new SeatOffer(
                        airbaseName,
                        $"{aircraft.definition.unitName}  ·  {pilot}  {SeatTable.Label(Role.Wso).ToUpperInvariant()}",
                        aircraft.persistentID,
                        state
                    )
                );
            }
        }

        _offers = [.. offers.OrderBy(x => x.Airbase).ThenBy(x => x.Text)];

        return _offers;
    }

    private void DescribeStations(Aircraft aircraft, List<(string, bool)> stations)
    {
        var mine = _session.LocalRole(aircraft) ?? Role.Pilot;
        var selected = mine == Role.Wso
            ? _session.BackSeat.Station
            : aircraft.weaponManager.currentWeaponStation?.Number ?? -1;
        var state = _session.Crew.ClientState(aircraft);
        var shared = Plugin.SeatTable.IsShared(aircraft);

        for (var i = 0; i < aircraft.weaponStations.Count; i++)
        {
            var weapon = aircraft.weaponStations[i].WeaponInfo;
            var holder = Plugin.SeatTable.Holder(aircraft, i, state);
            var label = shared && holder == Role.Pilot && state.PilotStation != i
                ? ""
                : SeatTable.Label(holder).ToUpperInvariant();
            var usable = Plugin.SeatTable.CanSelect(aircraft, mine, i, state);

            stations.Add(
                ($"{i}  {weapon.shortName}  {label}{(i == selected ? " >" : "")}", usable)
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
                _rows.Add(ScreenRow.CreateButton(answer, template, "ACCEPT", _session.Notices.Accept));
                _rows.Add(ScreenRow.CreateButton(answer, template, "DECLINE", _session.Notices.Decline));
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
                    () => _session.BackSeat.RequestLeave()
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
                        () => _session.RequestSeat(offer.AircraftId)
                    ).FitToText("REQUEST", "WAITING")
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
}