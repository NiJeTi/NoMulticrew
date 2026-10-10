using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew.Client.Ui;

internal sealed class CrewNotices
{
    private const float NoticeSeconds = 5f;
    private const float RefusalIntervalSeconds = 2f;

    private readonly ClientSession _session;

    private readonly List<(CrewJoinPrompt Prompt, float ExpiresAt)> _prompts = [];

    private string _toast = "";
    private float _toastUntil;
    private float _lastRefusal = float.NegativeInfinity;

    public CrewNotices(ClientSession session)
    {
        _session = session;
    }

    public CrewJoinPrompt? Pending
    {
        get
        {
            var now = Time.unscaledTime;

            foreach (var (prompt, expiresAt) in _prompts)
            {
                if (now <= expiresAt)
                {
                    return prompt;
                }
            }

            return null;
        }
    }

    public void Show(CrewJoinPrompt prompt)
    {
        var now = Time.unscaledTime;

        _prompts.RemoveAll(x => now > x.ExpiresAt);
        _prompts.Add((prompt, now + prompt.ExpiresInSeconds));

        ShowNotice(Texts.Requests.Incoming(CrewState.NameOf(prompt.JoinerPlayerIndex)));
        Feedback.Play(CrewCue.WeaponSwitch);
    }

    public void ShowNotice(string text)
    {
        var hud = SceneSingleton<CombatHUD>.i;
        var report = SceneSingleton<AircraftActionsReport>.i;
        if (hud != null && report != null && hud.aircraft != null)
        {
            report.ReportText(text, NoticeSeconds);
            return;
        }

        _toast = text;
        _toastUntil = Time.unscaledTime + NoticeSeconds;
    }

    public bool Refuse(string text)
    {
        Plugin.Logger.LogDebug($"Suppressed: {text}");

        if (Time.unscaledTime - _lastRefusal >= RefusalIntervalSeconds)
        {
            _lastRefusal = Time.unscaledTime;
            SceneSingleton<AircraftActionsReport>.i.ReportText(text, RefusalIntervalSeconds);
        }

        return false;
    }

    public bool RefuseStation(Aircraft aircraft, int station)
    {
        return Refuse(Texts.Weapons.HeldBy(_session.Crew.RoleHolding(aircraft, station)));
    }

    public bool RefuseSelection(Aircraft aircraft, int station)
    {
        var holder = _session.Crew.RoleHolding(aircraft, station);

        return Refuse(
            Plugin.SeatTable.IsShared(aircraft) ? Texts.Weapons.InUseBy(holder) : Texts.Weapons.ExclusiveTo(holder)
        );
    }

    public void Accept()
    {
        if (Pending is { } prompt)
        {
            Respond(prompt, accepted: true);
            Feedback.Play(CrewCue.WeaponSwitch);
        }
    }

    public void Decline()
    {
        if (Pending is { } prompt)
        {
            Respond(prompt, accepted: false);
            Feedback.Play(CrewCue.Deselect);
        }
    }

    public void Clear()
    {
        _prompts.Clear();
        _toastUntil = 0f;
    }

    public void Draw()
    {
        if (Time.unscaledTime < _toastUntil)
        {
            GUI.Label(new Rect(20f, Screen.height - 60f, 600f, 24f), _toast);
        }
    }

    private void Respond(CrewJoinPrompt prompt, bool accepted)
    {
        _prompts.RemoveAll(x => x.Prompt.RequestId == prompt.RequestId);

        _session.Send(new CrewJoinResponse(prompt.RequestId, accepted));
    }
}