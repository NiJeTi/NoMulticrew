using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace NoMulticrew;

internal static class Controls
{
    private const string ActionFire = "Fire";
    private const string ActionNextWeapon = "Next Weapon";
    private const string ActionPreviousWeapon = "Previous Weapon";
    private const string ActionEject = "Eject";

    public static bool IsEjectDown()
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField && player != null && player.GetButtonDown(ActionEject);
    }

    public static bool IsFireHeld()
    {
        var player = GameManager.playerInput;

        return !InputFieldChecker.InsideInputField
            && player != null
            && player.GetButton(ActionFire)
            && (!PlayerSettings.menuWeaponSafety || !Cursor.visible);
    }

    public static bool IsNextWeaponPressed()
    {
        return IsButtonClicked(ActionNextWeapon);
    }

    public static bool IsPreviousWeaponPressed()
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
}
