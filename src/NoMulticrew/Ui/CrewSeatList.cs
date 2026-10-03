using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Ui;

internal sealed class CrewSeatList
{
    private readonly struct Row
    {
        public readonly PersistentID AircraftId;
        public readonly byte SeatIndex;
        public readonly string Label;

        public Row(PersistentID aircraftId, byte seatIndex, string label)
        {
            AircraftId = aircraftId;
            SeatIndex = seatIndex;
            Label = label;
        }
    }

    private readonly ClientSession _session;

    private readonly List<Row> _rows = [];

    private bool _visible;

    public CrewSeatList(ClientSession session)
    {
        _session = session;
    }

    public void Refresh(Airbase airbase)
    {
        _rows.Clear();
        _visible = _session.Confirmed;

        if (!_visible || !GameManager.GetLocalPlayer<Player>(out var localPlayer))
        {
            return;
        }

        foreach (var aircraft in UnityEngine.Object.FindObjectsOfType<Aircraft>())
        {
            var seats = Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey);

            if (seats.Count == 0
                || !JoinRequests.CanBoard(aircraft, localPlayer, out _)
                || !JoinRequests.TryGetAirbase(aircraft, out var near)
                || near != airbase)
            {
                continue;
            }

            for (var index = 0; index < seats.Count; index++)
            {
                if (_session.Crew.IsTaken(aircraft.persistentID, index))
                {
                    continue;
                }

                _rows.Add(
                    new Row(
                        aircraft.persistentID,
                        (byte)index,
                        $"{aircraft.definition.unitName} — "
                        + $"{aircraft.Player.GetDisplayName(PlayerNameContext.Other)} ({seats[index].Role})"
                    )
                );
            }
        }
    }

    public void Draw()
    {
        if (!_visible || _rows.Count == 0)
        {
            return;
        }

        GUI.Box(new Rect(20f, 120f, 380f, 40f + _rows.Count * 28f), "Crew seats at this airbase");

        for (var i = 0; i < _rows.Count; i++)
        {
            if (GUI.Button(new Rect(30f, 152f + i * 28f, 360f, 24f), _rows[i].Label))
            {
                _session.Send(new MulticrewJoinRequest(_rows[i].AircraftId, _rows[i].SeatIndex));
            }
        }
    }

    public void Clear()
    {
        _rows.Clear();
        _visible = false;
    }
}
