namespace NoMulticrew.Networking.Messages;

internal enum CrewCommandKind : byte
{
    FiringState = 0,
    SingleFire = 1,
    StoppedFiring = 2,
    ClaimHit = 3,
    LaunchMissile = 4,
    TurretVector = 5,
    SetStationTargets = 6,
    SelectStation = 7,
}