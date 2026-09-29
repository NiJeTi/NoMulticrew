using BepInEx.Configuration;

namespace NoMulticrew;

internal sealed class Settings
{
    private Settings()
    {
    }

    public static Settings Init(ConfigFile config)
    {
        return new Settings();
    }
}