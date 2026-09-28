using NuclearOption.DedicatedServer.Commands;

namespace NoMulticrew.Server;

internal static class CrewServerCommands
{
    private const string Name = "crew";

    public static bool TryRegister(ServerSession session)
    {
        var instance = ServerRemoteCommands.Instance;

        if (instance == null)
        {
            return false;
        }

        if (instance.Commands.ContainsKey(Name))
        {
            return true;
        }

        instance.AddCommands([
            new ServerCommand(Name, (server, _) =>
            {
                var (ok, description) = server.RunOnMainThreadBlocking(() => (true, session.Describe()));

                return ok
                    ? CommandResponse.Create(StatusCode.Success, description)
                    : CommandResponse.Create(StatusCode.CommandError, "Could not read crew state on the main thread.");
            })
        ]);

        Plugin.Logger.LogInfo("Registered 'crew' server command");

        return true;
    }

    public static void Unregister()
    {
        ServerRemoteCommands.Instance?.Commands.Remove(Name);
    }
}
