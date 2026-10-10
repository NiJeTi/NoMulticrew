using NoMulticrew.Networking;
using NuclearOption.UIStyleSystem;
using UnityEngine;

namespace NoMulticrew.Client.Ui;

internal sealed class CrewNotices
{
    private const float NoticeSeconds = 5f;
    private const float RefusalIntervalSeconds = 2f;

    private readonly ClientSession _session;

    private readonly List<(CrewJoinPrompt Prompt, float ExpiresAt)> _prompts = [];

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

        ShowNotice(Texts.Requests.Incoming(CrewState.NameOf(prompt.JoinerPlayerIndex)), NoticeTone.Neutral);
        Feedback.Play(CrewCue.WeaponSwitch);
    }

    public void ShowNotice(string text, NoticeTone tone)
    {
        var report = SceneSingleton<AircraftActionsReport>.i;
        if (report != null)
        {
            report.ReportText(Colored(text, tone), NoticeSeconds);
        }
    }

    public bool Refuse(string text)
    {
        Plugin.Logger.LogDebug($"Suppressed: {text}");

        if (Time.unscaledTime - _lastRefusal >= RefusalIntervalSeconds)
        {
            _lastRefusal = Time.unscaledTime;
            SceneSingleton<AircraftActionsReport>.i.ReportText(
                Colored(text, NoticeTone.Negative), RefusalIntervalSeconds
            );
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
    }

    private static string Colored(string text, NoticeTone tone)
    {
        var theme = ThemeManager.Active.ColorTheme;

        return tone switch
        {
            NoticeTone.Positive => text.AddColor(theme.AllClear),
            NoticeTone.Caution => text.AddColor(theme.Warning),
            NoticeTone.Negative => text.AddColor(theme.Alert),
            _ => text,
        };
    }

    private void Respond(CrewJoinPrompt prompt, bool accepted)
    {
        _prompts.RemoveAll(x => x.Prompt.RequestId == prompt.RequestId);

        _session.Send(new CrewJoinResponse(prompt.RequestId, accepted));
    }
}