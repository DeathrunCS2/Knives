using Deathrun.Knives.Interfaces.Managers;
using Deathrun.Knives.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Deathrun.Knives.Managers;

internal static class ManagersDependencyInjection
{
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddSingleton<IManager, IKnivesManager, KnivesManager>();
            
        return services;
    }
}
