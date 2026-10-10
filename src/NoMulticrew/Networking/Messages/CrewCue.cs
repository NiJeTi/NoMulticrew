namespace NoMulticrew.Networking.Messages;

internal enum CrewCue : byte
{
    None = 0,
    Select = 1,
    Deselect = 2,
    WeaponSwitch = 3,
}