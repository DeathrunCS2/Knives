using Deathrun.Knives.Interfaces.Managers;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Deathrun.Knives.Interfaces.Managers.Native;

internal interface IEventManager : IManager
{
    delegate void DelegateOnEventFired(IGameEvent e);

    delegate HookReturnValue<bool> DelegateOnHookEvent(EventHookParams param);

    void HookEvent(string eventName, DelegateOnHookEvent callback);

    void ListenEvent(string eventName, DelegateOnEventFired callback);

    IGameEvent? CreateEvent(string eventName, bool force);

    T? CreateEvent<T>(bool force) where T : class, IGameEvent;
    
    /// <summary>
    /// Print a message to a ZPlayer's center HTML HUD (survival respawn status overlay)
    /// </summary>
    /// <param name="controller">The controller instance to send the message to</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in seconds to display the message (default: 5)</param>
    void PrintToCenterHtml(IPlayerController controller, string message, int duration = 5);
}

/// <summary>
/// Parameters for event hooks
/// </summary>
public class EventHookParams(IGameEvent e, bool serverOnly)
{
    public IGameEvent Event { get; } = e;
    public bool ServerOnly { get; private set; } = serverOnly;

    public void SetServerOnly()
        => ServerOnly = true;
}
