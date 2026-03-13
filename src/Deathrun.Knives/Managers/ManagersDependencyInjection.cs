using Deathrun.Knives.Interfaces.Managers.Native;
using Deathrun.Knives.Interfaces.Managers;
using Deathrun.Knives.Managers.Native.ClientListener;
using Deathrun.Knives.Managers.Native.Event;
using Deathrun.Knives.Managers.Native.GameListener;
using Deathrun.Knives.Extensions;
using Deathrun.Knives.Interfaces.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace Deathrun.Knives.Managers;

internal static class ManagersDependencyInjection
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        //Native Managers
        services.AddSingleton<IManager, IClientListenerManager, ClientListenerManager>();
        services.AddSingleton<IManager, IEventManager, EventManager>();
        services.AddSingleton<IManager, IGameListenerManager, GameListenerManager>();
        
        services.AddSingleton<IManager, IKnivesManager, KnivesManager>();
            
        return services;
    }
}
