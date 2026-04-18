using System;
using System.IO;
using Deathrun.Knives.Managers;
using Deathrun.Knives.Interfaces;
using Deathrun.Knives.Interfaces.Managers;
using DeathrunManager.Shared;
using DeathrunManager.Shared.DeathrunObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Abstractions;

namespace Deathrun.Knives;

public class Knives : IDeathrunModule
{
    public string Name         => $"Knives Extension";
    public string Author       => "AquaVadis";
    public IDeathrunManager DeathrunManager { get; }
    
    private readonly ServiceProvider  _serviceProvider;
    
    public Knives(ISharedSystem sharedSystem, IDeathrunManager deathrunManager)
    {
        DeathrunManager = deathrunManager;
        
        var services = new ServiceCollection();
        services.AddSingleton(deathrunManager);
        services.AddSingleton(sharedSystem);
        services.AddSingleton(sharedSystem.GetModSharp());
        services.AddSingleton(sharedSystem.GetHookManager());
        services.AddSingleton(sharedSystem.GetEntityManager());
        services.AddSingleton(sharedSystem.GetClientManager());
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        services.AddManagers();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    #region IModule
    
    public bool Init(bool hotReload)
    {
        //load managers
        CallInit<IManager>();
        return true;
    }

    public void PostInit(bool hotReload) { CallPostInit<IManager>(); }

    public void Shutdown(bool hotReload)
    {
        CallShutdown<IManager>();
    }

    public void OnAllModSharpModulesLoaded()
    {
        CallOnAllSharpModulesLoaded<IManager>();
    }
    
    #endregion
    
    #region Injected Instances' Caller methods
    
    private int CallInit<T>() where T : IBaseInterface
    {
        var init = 0;

        foreach (var service in _serviceProvider.GetServices<T>())
        {
            if (!service.Init())
            {
                Log(service.GetType().Name, "Failed to Init {service}!");

                return -1;
            }

            init++;
        }

        return init;
    }

    private void CallPostInit<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.OnPostInit();
            }
            catch (Exception e)
            {
                Log(service.GetType().Name, $"An error occurred while calling PostInit | {e}");
            }
        }
    }

    private void CallShutdown<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.Shutdown();
            }
            catch (Exception e)
            {
                Log(service.GetType().Name, $"An error occurred while calling Shutdown | {e}");
            }
        }
    }

    private void CallOnAllSharpModulesLoaded<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.OnAllSharpModulesLoaded();
            }
            catch (Exception e)
            {
                
                Log(service.GetType().Name, $"An error occurred while calling OnAllSharpModulesLoaded | {e}");
            }
        }
    }

    #endregion
    
    #region ColoredLog 
    
    private static void Log(string header, string message, 
                            ConsoleColor backgroundColor = ConsoleColor.DarkGray,
                            ConsoleColor textColor = ConsoleColor.Black)
    {
        Console.ForegroundColor = textColor;
        Console.BackgroundColor = backgroundColor;
        Console.Write($"{header}:");
        Console.ResetColor();
        Console.Write($" {message} \n");
    }
    
    #endregion
}