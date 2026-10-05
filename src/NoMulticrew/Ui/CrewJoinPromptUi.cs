using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Ui;

internal sealed class CrewJoinPromptUi
{
    private readonly ClientSession _session;

    private CrewJoinPrompt? _prompt;
    private float _expiresAt;

    private string _toast = "";
    private float _toastUntil;

    public CrewJoinPromptUi(ClientSession session)
    {
        _session = session;
    }

    public CrewJoinPrompt? Pending => _prompt;

    public void Show(CrewJoinPrompt prompt)
    {
        _prompt = prompt;
        _expiresAt = Time.timeSinceLevelLoad + prompt.ExpiresInSeconds;

        ShowNotice($"{NameOf(prompt.JoinerPlayerIndex)} wants {SeatLabel(prompt)} — open the map to answer");
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
        _toastUntil = Time.timeSinceLevelLoad + 5f;
    }

    public void Accept()
    {
        if (_prompt is { } prompt)
        {
            Respond(prompt, accepted: true);
        }
    }

    public void Decline()
    {
        if (_prompt is { } prompt)
        {
            Respond(prompt, accepted: false);
        }
    }

    public void Tick()
    {
        if (_prompt != null && Time.timeSinceLevelLoad > _expiresAt)
        {
            _prompt = null;
        }
    }

    public void Draw()
    {
        if (Time.timeSinceLevelLoad < _toastUntil)
        {
            GUI.Label(new Rect(20f, Screen.height - 60f, 600f, 24f), _toast);
        }
    }

    private void Respond(CrewJoinPrompt prompt, bool accepted)
    {
        _prompt = null;

        _session.Send(new CrewJoinResponse(prompt.RequestId, accepted));
    }

    public static string SeatLabel(CrewJoinPrompt prompt)
    {
        return GameManager.GetLocalAircraft(out var own)
            ? Plugin.SeatTable.Label(own.definition.jsonKey, prompt.SeatIndex)
            : $"seat {prompt.SeatIndex}";
    }

    public static string NameOf(int playerIndex)
    {
        foreach (var player in UnityEngine.Object.FindObjectsOfType<Player>())
        {
            if (player.PlayerIndex == playerIndex)
            {
                return player.GetDisplayName(PlayerNameContext.Other);
            }
        }

        return $"Player {playerIndex}";
    }
}