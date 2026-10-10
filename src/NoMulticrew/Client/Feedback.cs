using HarmonyLib;
using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew.Client;

internal static class Feedback
{
    private static readonly AccessTools.FieldRef<CombatHUD, AudioClip> SelectSoundRef =
        AccessTools.FieldRefAccess<CombatHUD, AudioClip>("selectSound");

    private static readonly AccessTools.FieldRef<CombatHUD, AudioClip> DeselectSoundRef =
        AccessTools.FieldRefAccess<CombatHUD, AudioClip>("deselectSound");

    private static readonly AccessTools.FieldRef<CombatHUD, AudioClip> WeaponSwitchSoundRef =
        AccessTools.FieldRefAccess<CombatHUD, AudioClip>("weaponSwitchSound");

    public static void Play(CrewCue cue)
    {
        var hud = SceneSingleton<CombatHUD>.i;
        if (hud == null)
        {
            return;
        }

        var clip = cue switch
        {
            CrewCue.Select => SelectSoundRef(hud),
            CrewCue.Deselect => DeselectSoundRef(hud),
            CrewCue.WeaponSwitch => WeaponSwitchSoundRef(hud),
            _ => null,
        };

        if (clip != null)
        {
            SoundManager.PlayInterfaceOneShot(clip);
        }
    }
}