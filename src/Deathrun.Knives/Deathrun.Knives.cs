using System;
using System.IO;
using Deathrun.Knives.Managers;
using Deathrun.Knives.Interfaces;
using Deathrun.Knives.Interfaces.Managers;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Objects;
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
    
    private readonly ServiceProvider  _serviceProvider;
    
#pragma warning disable CA2211
    public IDeathrunManager DeathrunManagerApi { get; } = null!;
#pragma warning restore CA2211
    
    private static ILogger<Knives> _logger                 = null!;
    
    public Knives(ISharedSystem sharedSystem, IDeathrunManager deathrunManagerApi)
    {
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<Knives>();
        
        var services = new ServiceCollection();
        
        services.AddSingleton(deathrunManagerApi);
        services.AddSingleton(sharedSystem);
        services.AddSingleton(sharedSystem.GetModSharp());
        services.AddSingleton(sharedSystem.GetHookManager());
        services.AddSingleton(sharedSystem.GetEntityManager());
        services.AddSingleton(sharedSystem.GetClientManager());
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.AddSingleton(sharedSystem);
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        
        services.AddManagers();
        _serviceProvider = services.BuildServiceProvider();
    }

    #region IModule
    
    public bool Init()
    {
        _logger.LogInformation("[Deathrun.Knives] {colorMessage}", "Load Deathrun Knives!");
        
        //load managers
        CallInit<IManager>();
        return true;
    }

    public void PostInit() { CallPostInit<IManager>(); }

    public void Shutdown()
    {
        CallShutdown<IManager>();

        _serviceProvider.ShutdownAllSharpExtensions();
        
        _logger.LogInformation("[Deathrun.Knives] {colorMessage}", "Unloaded Deathrun Knives!");
    }

    public void OnAllModulesLoaded()
    {
        
        CallOnAllSharpModulesLoaded<IManager>();
    }
    
    public bool Reload(IServiceProvider serviceProvider) { return true; }
    
    #endregion
    
    #region Injected Instances' Caller methods
    
    private int CallInit<T>() where T : IBaseInterface
    {
        var init = 0;

        foreach (var service in _serviceProvider.GetServices<T>())
        {
            if (!service.Init())
            {
                _logger.LogError("Failed to Init {service}!", service.GetType().FullName);

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
                _logger.LogError(e, "An error occurred while calling PostInit in {m}", service.GetType().Name);
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
                _logger.LogError(e, "An error occurred while calling Shutdown in {m}", service.GetType().Name);
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
                _logger.LogError(e, "An error occurred while calling OnAllSharpModulesLoaded in {m}", service.GetType().Name);
            }
        }
    }

    #endregion
}