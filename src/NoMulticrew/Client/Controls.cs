using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace NoMulticrew.Client;

internal static class Controls
{
    private const string ActionFire = "Fire";
    private const string ActionNextWeapon = "Next Weapon";
    private const string ActionPreviousWeapon = "Previous Weapon";
    private const string ActionEject = "Eject";

    public static bool IsEjectDown()
    {
        return !InputFieldChecker.InsideInputField && GameManager.playerInput.GetButtonDown(ActionEject);
    }

    public static bool IsFireHeld()
    {
        return !InputFieldChecker.InsideInputField
            && GameManager.playerInput.GetButton(ActionFire)
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
        return !InputFieldChecker.InsideInputField
            && GameManager.playerInput.GetButtonTimedPressUp(action, 0f, PlayerSettings.clickDelay);
    }
}