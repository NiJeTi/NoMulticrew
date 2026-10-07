using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Marks;

internal sealed class CrewMarks : IDisposable
{
    private const float LineThickness = 2f;
    private const float LineAlpha = 0.6f;

    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> MarkersRef =
        AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");

    private static readonly Action<HUDUnitMarker> UpdateMarkerColor = AccessTools.MethodDelegate<Action<HUDUnitMarker>>(
        GameMembers.Method(typeof(HUDUnitMarker), "UpdateColor")
    );

    private static readonly Action<HUDUnitMarker> SetMarkerFactionColor = AccessTools.MethodDelegate<Action<HUDUnitMarker>>(
        GameMembers.Method(typeof(HUDUnitMarker), "SetFactionColor")
    );

    private readonly ClientSession _session;

    private readonly HashSet<PersistentID> _targets = [];
    private readonly HashSet<PersistentID> _next = [];
    private readonly List<(GameObject Instance, Image Image)> _lines = [];

    private Aircraft? _aircraft;

    public CrewMarks(ClientSession session)
    {
        _session = session;
    }

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            if (line.Instance != null)
            {
                Object.Destroy(line.Instance);
            }
        }

        _lines.Clear();
    }

    public bool TryGetColor(Unit unit, out Color color)
    {
        color = default;

        if (unit == null || !_targets.Contains(unit.persistentID))
        {
            return false;
        }

        color = Plugin.Palette.Active;
        return true;
    }

    public void Tick()
    {
        Collect();

        if (!_next.SetEquals(_targets))
        {
            _targets.Clear();
            _targets.UnionWith(_next);
            Repaint();
        }

        DrawLines();
    }

    public void Repaint()
    {
        var failures = 0;
        Exception? last = null;

        var hud = SceneSingleton<CombatHUD>.i;
        if (hud != null)
        {
            foreach (var marker in MarkersRef(hud))
            {
                try
                {
                    SetMarkerFactionColor(marker);
                    UpdateMarkerColor(marker);
                }
                catch (Exception e)
                {
                    failures++;
                    last = e;
                }
            }
        }

        var map = SceneSingleton<DynamicMap>.i;
        if (map != null)
        {
            foreach (var icon in map.mapIcons)
            {
                try
                {
                    icon.UpdateColor();
                }
                catch (Exception e)
                {
                    failures++;
                    last = e;
                }
            }
        }

        if (failures > 0)
        {
            Plugin.Logger.LogError($"Failed to repaint {failures} crew marks: {last}");
        }
    }

    private void Collect()
    {
        var seat = _session.BackSeat;

        if (seat.Aircraft != null)
        {
            _aircraft = seat.Aircraft;
            _session.Crew.CollectCrewmateTargets(seat.Aircraft, seat.SeatIndex, _next);
        }
        else if (GameManager.GetLocalAircraft(out var own))
        {
            _aircraft = own;
            _session.Crew.CollectCrewmateTargets(own, -1, _next);
        }
        else
        {
            _aircraft = null;
            _next.Clear();
        }

        _next.RemoveWhere(static id =>
            !UnitRegistry.TryGetUnit(id, out var unit)
            || (unit.NetworkHQ != null
                && SceneSingleton<DynamicMap>.i != null
                && unit.NetworkHQ == SceneSingleton<DynamicMap>.i.HQ));
    }

    private void DrawLines()
    {
        var used = 0;
        var map = SceneSingleton<DynamicMap>.i;

        if (map != null
            && map.gameObject.activeInHierarchy
            && map.mapImage.transform.localScale.x > 0f
            && _targets.Count > 0
            && _aircraft != null
            && map.TryGetIcon(_aircraft, out var from))
        {
            var scale = map.mapImage.transform.localScale.x;
            var color = Plugin.Palette.Active;
            color.a *= LineAlpha;

            foreach (var id in _targets)
            {
                if (!UnitRegistry.TryGetUnit(id, out var unit) || !map.TryGetIcon(unit, out var to))
                {
                    continue;
                }

                Draw(Line(used++, map), from.transform.localPosition, to.transform.localPosition, color, LineThickness / scale);
            }
        }

        for (var i = used; i < _lines.Count; i++)
        {
            if (_lines[i].Instance != null)
            {
                _lines[i].Instance.SetActive(false);
            }
        }
    }

    private (GameObject Instance, Image Image) Line(int index, DynamicMap map)
    {
        if (index < _lines.Count && _lines[index].Instance != null)
        {
            return _lines[index];
        }

        var instance = Object.Instantiate(map.mapWaypointVector, map.iconLayer.transform);
        instance.name = "NoMulticrew.CrewTargetLine";
        instance.transform.SetAsFirstSibling();

        var image = instance.GetComponentInChildren<Image>(true);
        image.raycastTarget = false;

        var line = (instance, image);

        if (index < _lines.Count)
        {
            _lines[index] = line;
        }
        else
        {
            _lines.Add(line);
        }

        return line;
    }

    private static void Draw((GameObject Instance, Image Image) line, Vector3 source, Vector3 target, Color color, float thickness)
    {
        var delta = target - source;
        var transform = line.Instance.transform;

        line.Instance.SetActive(true);
        transform.localPosition = source;
        transform.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(thickness, delta.magnitude, thickness);
        line.Image.color = color;
    }
}
