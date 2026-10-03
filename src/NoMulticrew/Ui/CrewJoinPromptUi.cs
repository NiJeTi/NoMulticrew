using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Ui;

internal sealed class CrewJoinPromptUi
{
    private readonly ClientSession _session;

    private MulticrewJoinPrompt? _prompt;
    private float _expiresAt;

    private string _toast = "";
    private float _toastUntil;

    public CrewJoinPromptUi(ClientSession session)
    {
        _session = session;
    }

    public void Show(MulticrewJoinPrompt prompt)
    {
        _prompt = prompt;
        _expiresAt = Time.timeSinceLevelLoad + prompt.ExpiresInSeconds;
    }

    public void ShowNotice(string text)
    {
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

        if (_prompt is not { } prompt)
        {
            return;
        }

        var remaining = Mathf.Max(0f, _expiresAt - Time.timeSinceLevelLoad);
        var box = new Rect(Screen.width * 0.5f - 200f, 80f, 400f, 96f);

        GUI.Box(box, $"{NameOf(prompt.JoinerPlayerIndex)} wants the back seat  ({remaining:F0}s)");

        if (GUI.Button(new Rect(box.x + 20f, box.y + 50f, 170f, 30f), "Accept"))
        {
            Respond(prompt, accepted: true);
        }

        if (GUI.Button(new Rect(box.x + 210f, box.y + 50f, 170f, 30f), "Decline"))
        {
            Respond(prompt, accepted: false);
        }
    }

    private void Respond(MulticrewJoinPrompt prompt, bool accepted)
    {
        _prompt = null;

        _session.Send(new MulticrewJoinResponse(prompt.RequestId, accepted));
    }

    private static string NameOf(int playerIndex)
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
