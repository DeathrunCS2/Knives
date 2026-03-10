
using Deathrun.Speedometer.Interfaces.Managers;

namespace Deathrun.Knives.Interfaces.Managers.Native;

/// <summary>
/// Interface for game lifecycle event listeners
/// </summary>
internal interface IGameListenerManager : IManager
{
    delegate void ListenerDelegate();

    event ListenerDelegate? GameInit;
    event ListenerDelegate? GameActivate;
    event ListenerDelegate? GameShutdown;
    event ListenerDelegate? GamePreShutdown;
    event ListenerDelegate? ResourcePrecache;
    event ListenerDelegate? RoundRestartPre;
    event ListenerDelegate? RoundRestartPost;
    event ListenerDelegate? ServerActive;
}
