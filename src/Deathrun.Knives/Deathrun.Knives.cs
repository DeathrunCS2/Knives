using System;
using System.IO;
using Deathrun.Knives.Managers;
using Deathrun.Knives.Interfaces;
using Deathrun.Knives.Interfaces.Managers;
using DeathrunManager.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Abstractions;

namespace Deathrun.Knives;

public class Knives : IModSharpModule
{
    public string DisplayName         => $"[Deathrun][Module] Knives - Build #{Bridge.BuildNumber} - Last Build Time: {Bridge.FileTime}";
    public string DisplayAuthor       => "AquaVadis";
    
    private readonly ServiceProvider  _serviceProvider;
    
#pragma warning disable CA2211
    public static IDeathrunManager DeathrunManagerApi      = null!;
    public static InterfaceBridge Bridge                   = null!;
    public static string ModulePath                        = "";
#pragma warning restore CA2211
    
    private static ILogger<Knives> _logger                 = null!;
    
    public Knives(ISharedSystem sharedSystem,
        string                   dllPath,
        string                   sharpPath,
        Version                  version,
        IConfiguration           coreConfiguration,
        bool                     hotReload)
    {
        ModulePath = dllPath;
        Bridge = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<Knives>();
        
        var configuration = new ConfigurationBuilder()
                                .AddJsonFile(Path.Combine(dllPath, "base.json"), true, false)
                                .Build();
        
        var services = new ServiceCollection();

        services.AddSingleton(Bridge);
        services.AddSingleton(Bridge.ModSharp);
        services.AddSingleton(Bridge.HookManager);
        services.AddSingleton(Bridge.EntityManager);
        services.AddSingleton(Bridge.ClientManager);
        services.AddSingleton(Bridge.LoggerFactory);
        services.AddSingleton(sharedSystem);
        services.AddSingleton<IConfiguration>(configuration);
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
        DeathrunManagerApi 
            = Bridge
                .SharpModuleManager
                .GetOptionalSharpModuleInterface<IDeathrunManager>(IDeathrunManager.Identity)?
                .Instance ?? throw new Exception("Failed to capture Deathrun Manager Api!");
        
        CallOnAllSharpModulesLoaded<IManager>();
    }

    public void OnLibraryConnected(string name) { }

    public void OnLibraryDisconnect(string name) { }
    
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