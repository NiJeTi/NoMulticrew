using InputFramework;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;

namespace NoMulticrew;

internal sealed class Controls
{
    public const string GuidInputFramework = "experimental.assassin1076.extrainputframework";

    private const string ActionCategory = "Gameplay";
    private const string ActionFire = "Fire";
    private const string ActionNextWeapon = "Next Weapon";
    private const string ActionPreviousWeapon = "Previous Weapon";
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

    public bool IsFireHeld()
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField
            && player != null
            && player.GetButton(ActionFire)
            && (!PlayerSettings.menuWeaponSafety || !Cursor.visible);
    }

    public bool IsNextWeaponPressed()
    {
        return IsButtonClicked(ActionNextWeapon);
    }

    public bool IsPreviousWeaponPressed()
    {
        return IsButtonClicked(ActionPreviousWeapon);
    }

    private static bool IsButtonClicked(string action)
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField
            && player != null
            && player.GetButtonTimedPressUp(action, 0f, PlayerSettings.clickDelay);
    }

    private static bool IsButtonDown(string action)
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField && player != null && player.GetButtonDown(action);
    }
}