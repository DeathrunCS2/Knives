using System;
using System.IO;
using Deathrun.Knives.Managers;
using Deathrun.Speedometer.Interfaces;
using Deathrun.Speedometer.Interfaces.Managers;
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
    public string DisplayName         => $"[Deathrun] Knives - Last Build Time: {Bridge.FileTime}";
    public string DisplayAuthor       => "AquaVadis";
    
    private readonly ServiceProvider  _serviceProvider;
    private static ISharedSystem      _sharedSystem = null!;
    public static ISharedSystem SharedSystem => _sharedSystem;
    
    public static IModSharpModuleInterface<IDeathrunManager>?  DeathrunManagerApi;
    
#pragma warning disable CA2211

    public static string ModulePath                 = "";
    public static ILogger<Knives> Logger   = null!;
    public static InterfaceBridge Bridge            = null!;
    public static Knives Instance          = null!;
    
#pragma warning restore CA2211
    
    public Knives(ISharedSystem sharedSystem,
        string                   dllPath,
        string                   sharpPath,
        Version                  version,
        IConfiguration           coreConfiguration,
        bool                     hotReload)
    {
        ModulePath = dllPath;
        Bridge = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        Instance = this;
        Logger = sharedSystem.GetLoggerFactory().CreateLogger<Knives>();
        _sharedSystem = sharedSystem;
        
        var configuration = new ConfigurationBuilder()
                                .AddJsonFile(Path.Combine(dllPath, "base.json"), true, false)
                                .Build();
        
        var services = new ServiceCollection();

        services.AddSingleton(Bridge);
        services.AddSingleton(Bridge.ClientManager);
        services.AddSingleton(sharedSystem);
        services.AddSingleton(sharedSystem.GetConVarManager());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        
        services.AddManagers();
        _serviceProvider = services.BuildServiceProvider();
    }

    #region IModule
    
    public bool Init()
    {
        Logger.LogInformation("[Deathrun.Knives] {colorMessage}", "Load Deathrun Knives!");
        
        //load managers
        CallInit<IManager>();
        return true;
    }

    public void PostInit() { CallPostInit<IManager>(); }

    public void Shutdown()
    {
        CallShutdown<IManager>();

        _serviceProvider.ShutdownAllSharpExtensions();
        
        Logger.LogInformation("[Deathrun.Knives] {colorMessage}", "Unloaded Deathrun Knives!");
    }

    public void OnAllModulesLoaded()
    {
        DeathrunManagerApi 
            = Bridge.SharpModuleManager.GetOptionalSharpModuleInterface<IDeathrunManager>(IDeathrunManager.Identity);
        
        if (DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            Logger.LogInformation("[Deathrun.Knives] {colorMessage}", "Captured Deathrun Manager Api!");
        }
        else
        {
            Logger.LogError("Failed to capture Deathrun Manager Api!");
            return;
        }

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
                Logger.LogError("Failed to Init {service}!", service.GetType().FullName);

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
                Logger.LogError(e, "An error occurred while calling PostInit in {m}", service.GetType().Name);
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
                Logger.LogError(e, "An error occurred while calling Shutdown in {m}", service.GetType().Name);
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
                Logger.LogError(e, "An error occurred while calling OnAllSharpModulesLoaded in {m}", service.GetType().Name);
            }
        }
    }

    #endregion
}