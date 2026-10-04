using InputFramework;
using NuclearOption.MissionEditorScripts;
using Rewired;

namespace NoMulticrew;

internal sealed class Controls
{
    public const string GuidInputFramework = "experimental.assassin1076.extrainputframework";

    private const string ActionCategory = "Gameplay";
    private const string ActionLeaveSeat = "NoMulticrew: Leave crew seat";
    private const string ActionAcceptRequest = "NoMulticrew: Accept crew request";
    private const string ActionDeclineRequest = "NoMulticrew: Decline crew request";

    private Controls()
    {
    }

    public static Controls Init()
    {
        ExtraInputManager.LoadPendingActions();
        ExtraInputManager.RegisterAction(ActionLeaveSeat, InputActionType.Button, ActionCategory);
        ExtraInputManager.RegisterAction(ActionAcceptRequest, InputActionType.Button, ActionCategory);
        ExtraInputManager.RegisterAction(ActionDeclineRequest, InputActionType.Button, ActionCategory);

        return new Controls();
    }

    public bool IsLeaveSeatDown()
    {
        return IsButtonDown(ActionLeaveSeat);
    }

    public bool IsAcceptRequestDown()
    {
        return IsButtonDown(ActionAcceptRequest);
    }

    public bool IsDeclineRequestDown()
    {
        return IsButtonDown(ActionDeclineRequest);
    }

    private static bool IsButtonDown(string action)
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField && player != null && player.GetButtonDown(action);
    }
}