using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew.Ui;

internal sealed class CrewJoinPromptUi
{
    private readonly ClientSession _session;

    private readonly List<(CrewJoinPrompt Prompt, float ExpiresAt)> _prompts = [];

    private string _toast = "";
    private float _toastUntil;

    public CrewJoinPromptUi(ClientSession session)
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
        _prompts.Add((prompt, Time.unscaledTime + prompt.ExpiresInSeconds));

        ShowNotice($"{CrewState.NameOf(prompt.JoinerPlayerIndex)} wants {SeatTable.Label(Role.Wso)} — open the map to answer");
        Feedback.Play(CrewCue.WeaponSwitch);
    }

    public void ShowNotice(string text)
    {
        var hud = SceneSingleton<CombatHUD>.i;
        var report = SceneSingleton<AircraftActionsReport>.i;
        if (hud != null && report != null && hud.aircraft != null)
        {
            report.ReportText(text, 5f);
            return;
        }

        _toast = text;
        _toastUntil = Time.unscaledTime + 5f;
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

    public void Tick()
    {
        var now = Time.unscaledTime;

        _prompts.RemoveAll(x => now > x.ExpiresAt);
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