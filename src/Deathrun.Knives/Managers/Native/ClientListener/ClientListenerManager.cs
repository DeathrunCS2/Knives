using Deathrun.Knives.Interfaces.Managers.Native;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace Deathrun.Knives.Managers.Native.ClientListener;

internal sealed class ClientListenerManager(InterfaceBridge bridge) : IClientListenerManager, IClientListener
{
    public event IClientListenerManager.ClientPreAdminCheckDelegate? ClientPreAdminCheck;
    public event IClientListenerManager.ClientDelegate? ClientConnected;
    public event IClientListenerManager.ClientDelegate? ClientPutInServer;
    public event IClientListenerManager.ClientDelegate? ClientPostAdminCheck;
    public event IClientListenerManager.ClientDisconnectDelegate? ClientDisconnecting;
    public event IClientListenerManager.ClientDisconnectDelegate? ClientDisconnected;
    public event IClientListenerManager.ClientDelegate? ClientSettingChanged;
    public event IClientListenerManager.ClientSayDelegate? ClientSayCommand;

    public bool OnClientPreAdminCheck(IGameClient client)
    {
        if (ClientPreAdminCheck == null)
            return false;

        // Invoke all subscribers and return true if any block the admin check
        var delegates = ClientPreAdminCheck.GetInvocationList();
        foreach (var @delegate in delegates)
        {
            var handler = (IClientListenerManager.ClientPreAdminCheckDelegate)@delegate;
            if (handler(client))
                return true;
        }

        return false;
    }

    public void OnClientConnected(IGameClient client)
        => ClientConnected?.Invoke(client);

    public void OnClientPutInServer(IGameClient client)
        => ClientPutInServer?.Invoke(client);

    public void OnClientPostAdminCheck(IGameClient client)
        => ClientPostAdminCheck?.Invoke(client);

    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason)
        => ClientDisconnecting?.Invoke(client, reason);

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
        => ClientDisconnected?.Invoke(client, reason);

    public void OnClientSettingChanged(IGameClient client)
        => ClientSettingChanged?.Invoke(client);

    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        if (ClientSayCommand == null)
            return ECommandAction.Skipped;

        var result = ECommandAction.Skipped;
        var delegates = ClientSayCommand.GetInvocationList();
        
        foreach (var @delegate in delegates)
        {
            var handler = (IClientListenerManager.ClientSayDelegate)@delegate;
            var action = handler(client, teamOnly, isCommand, commandName, message);
            
            // Priority: Handled > Stopped > Skipped
            if (action == ECommandAction.Handled)
                result = ECommandAction.Handled;
            else if (action == ECommandAction.Stopped && result == ECommandAction.Skipped)
                result = ECommandAction.Stopped;
        }

        return result;
    }

    public bool Init()
    {
        bridge.ClientManager.InstallClientListener(this);

        return true;
    }

    public void Shutdown()
    {
        bridge.ClientManager.RemoveClientListener(this);
    }

    public int ListenerVersion => IClientListener.ApiVersion;
    public int ListenerPriority => 0;
}
