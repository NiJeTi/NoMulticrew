using NoMulticrew.Networking;
using NuclearOption.DedicatedServer.Commands;

namespace NoMulticrew.Server;

internal static class CrewServerCommands
{
    public static bool EnsureRegistered()
    {
        var instance = ServerRemoteCommands.Instance;

        if (instance == null)
        {
            return false;
        }

        if (instance.Commands.ContainsKey("crew"))
        {
            return true;
        }

        instance.AddCommands([
            new ServerCommand("crew", (server, _) =>
            {
                var (ok, description) = server.RunOnMainThreadBlocking(() => (true, CrewConnections.Describe()));

                return ok
                    ? CommandResponse.Create(StatusCode.Success, description)
                    : CommandResponse.Create(StatusCode.CommandError, "Could not read crew state on the main thread.");
            })
        ]);

        Plugin.Logger.LogInfo("Registered 'crew' server command");

        return true;
    }
}
