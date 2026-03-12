using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deathrun.Knives.Extensions;
using Deathrun.Knives.Interfaces.Managers.SpeedManager;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.HookParams;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Deathrun.Knives.Managers.KnivesManager;

internal class KnivesManager(
    IModSharp modSharp,
    IHookManager hookManager,
    IEntityManager entityManager,
    IClientManager clientManager) : IKnivesManager, IGameListener
{
    private static IGlobalVars? _globalVars = null;
    public static KnivesConfig Config = null!;
    
    public static readonly ConcurrentDictionary<IDeathrunPlayer, Knife> DeathrunPlayersKnives = [];
    
    private bool _addCommandAnnouncers = false;
    
    #region IModule
    
    public bool Init()
    {
        LoadKnivesConfig();
        
        hookManager.PlayerSpawnPost.InstallForward(PlayerSpawnPost);
        hookManager.PlayerSwitchWeapon.InstallForward(PlayerSwitchWeapon);
        hookManager.PlayerEquipWeapon.InstallForward(PlayerEquipWeapon);
        hookManager.PlayerDispatchTraceAttack.InstallHookPre(PlayerDispatchTraceAttackPre);
        hookManager.PlayerPostThink.InstallForward(PlayerPostThink);
        hookManager.PlayerGetMaxSpeed.InstallHookPre(PlayerGetMaxSpeedPre);
        
        modSharp.InstallGameListener(this);
        
        clientManager.InstallCommandCallback("knife", OnClientKnivesCommand);
        clientManager.InstallCommandCallback("knives", OnClientKnivesCommand);
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        hookManager.PlayerSpawnPost.RemoveForward(PlayerSpawnPost);
        hookManager.PlayerSwitchWeapon.RemoveForward(PlayerSwitchWeapon);
        hookManager.PlayerEquipWeapon.RemoveForward(PlayerEquipWeapon);
        hookManager.PlayerDispatchTraceAttack.RemoveHookPre(PlayerDispatchTraceAttackPre);
        hookManager.PlayerPostThink.RemoveForward(PlayerPostThink);
        hookManager.PlayerGetMaxSpeed.RemoveHookPre(PlayerGetMaxSpeedPre);
        
        modSharp.RemoveGameListener(this);
        
        clientManager.RemoveCommandCallback("knife", OnClientKnivesCommand);
        clientManager.RemoveCommandCallback("knives", OnClientKnivesCommand);
    }

    #endregion

    #region Hooks

    private static void PlayerSpawnPost(IPlayerSpawnForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer?.IsValid is not true || deathrunPlayer.PlayerPawn is null) return;
        
        deathrunPlayer.ResetKnifeAbilityStates();
        
        switch (deathrunPlayer.GetKnife()?.Identifier)
        {
            case "pocket":
                deathrunPlayer.PlayerPawn.VelocityModifier = deathrunPlayer.GetKnife()?.Value ?? 1;
                break;
            case "butcher":
                deathrunPlayer.PlayerPawn.SetGravityScale(deathrunPlayer.GetKnife()?.Value ?? 1);
                break;
        }
    }
    
    private static void PlayerEquipWeapon(IPlayerEquipWeaponForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var switchedToWeapon = parms.Weapon;
        if (switchedToWeapon?.IsValidEntity is not true) return;

        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;

        UpdatePlayerKnifeAbility(deathrunPlayer, switchedToWeapon);
    }
    
    private static void PlayerSwitchWeapon(IPlayerSwitchWeaponForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var switchedToWeapon = parms.Weapon;
        if (switchedToWeapon?.IsValidEntity is not true) return;

        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;

        UpdatePlayerKnifeAbility(deathrunPlayer, switchedToWeapon);
    }
    
    private HookReturnValue<long> PlayerDispatchTraceAttackPre(IPlayerDispatchTraceAttackHookParams parms, HookReturnValue<long> result)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return default;
        
        var attackerClient = entityManager.FindEntityByHandle(parms.AttackerPawnHandle)?.GetController()?.GetGameClient();
        if (attackerClient?.IsValid is not true) return default;
            
        var attackerDeathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(attackerClient);
            
        if (attackerDeathrunPlayer?.GetKnife()?.Identifier.Equals("machete", StringComparison.OrdinalIgnoreCase) is true)
        {
            parms.Damage = (int)(parms.Damage * attackerDeathrunPlayer.GetKnife()?.Value ?? 1);
        }
        
        return default;
    }

    private static void PlayerPostThink(IPlayerThinkForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;
            
        DeathrunPlayersKnives.TryAdd(deathrunPlayer, Config.Knives[0]);
            
        deathrunPlayer.SetCenterMenuTopRowHtml
        (
            $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#A7A7A7'>Knife: </font>"
            + $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#efbfff'>{DeathrunPlayersKnives[deathrunPlayer].Name}</font>"    
        );
        
        //limit to every 6 seconds
        if (_globalVars?.TickCount % 384 is not 0) return;
        
        //skip if the player's knife is not the default type
        if (deathrunPlayer.GetKnife()?.Identifier.Equals("default") is not true) return;
            
        //skip invalid/dead players
        if (deathrunPlayer.IsValidAndAlive is not true || deathrunPlayer.PlayerPawn is null) return;
        
        if (deathrunPlayer.PlayerPawn.Health >= 100) return;
        
        var activeWeapon = deathrunPlayer.PlayerPawn.GetActiveWeapon();
        if (activeWeapon?.IsValidEntity is not true || activeWeapon.Classname.Contains("knife") is not true) return;

        deathrunPlayer.PlayerPawn.Health += (int) (deathrunPlayer.GetKnife()?.Value ?? 3);
    }
    
    private static HookReturnValue<float> PlayerGetMaxSpeedPre(IPlayerGetMaxSpeedHookParams parms, HookReturnValue<float> original)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return default;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        
        var activeWeapon = deathrunPlayer?.PlayerPawn?.GetActiveWeapon();
        if (activeWeapon?.IsValidEntity is not true 
            || activeWeapon.Classname.Contains("knife") is not true) return default;
        
        return deathrunPlayer?.GetKnife()?.Identifier switch
        {
            "machete" => new (EHookAction.SkipCallReturnOverride, 0.8f * 250),
            "pocket" => new (EHookAction.SkipCallReturnOverride,
                (deathrunPlayer?.GetKnife()?.Value ?? 1) * 250),
            _ => default
        };
    }

    #endregion
    
    #region Listeners
    public void OnServerInit() => _globalVars = modSharp.GetGlobals();
    
    public void OnGameActivate()
    {
        if (_addCommandAnnouncers is true) return;
        
        modSharp.PushTimer(() =>
        {
            DeathrunPlayerExtensions.SendColoredAllChatMessage("You can select a knife by typing {GREEN}/knife {DEFAULT}or {GREEN}/knives in the chat!");
        }, Random.Shared.Next(25), GameTimerFlags.StopOnMapEnd);
        
        _addCommandAnnouncers = true;
    }

    public void OnGameDeactivate()
    {
        _addCommandAnnouncers = false;
    }

    #endregion
    
    #region State update method/s

    private static void UpdatePlayerKnifeAbility(IDeathrunPlayer deathrunPlayer, IBaseWeapon currentWeapon)
    {
        if (deathrunPlayer?.IsValidAndAlive is not true || deathrunPlayer.PlayerPawn is null) return;

        if (currentWeapon.Classname.Contains("knife") is not true)
        {
            deathrunPlayer.ResetKnifeAbilityStates();
            return;
        }
        
        switch (deathrunPlayer.GetKnife()?.Identifier)
        {
            case "pocket":
                deathrunPlayer.PlayerPawn.VelocityModifier = deathrunPlayer.GetKnife()?.Value ?? 1;
                break;
            case "butcher":
                deathrunPlayer.PlayerPawn.SetGravityScale(deathrunPlayer.GetKnife()?.Value ?? 1);
                break;
        }
    }
    
    #endregion
    
    #region Commands
    
    private static ECommandAction OnClientKnivesCommand(IGameClient client, StringCommand command)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return ECommandAction.Stopped;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return ECommandAction.Stopped;

        if (command.ArgCount is 0)
        {
            deathrunPlayer.SendColoredChatMessage("{DEFAULT}Knives List(Special trait applies only when holding the knife): ");
            deathrunPlayer.SendColoredChatMessage("{GREY}Example(type in chat): /knife butcher");
            var index = 1;
            foreach (var knife in Config.Knives)
            {
                deathrunPlayer.SendColoredChatMessage(
                    $"{{GREEN}}{index}. {{DEFAULT}}{knife.Name} | {{GREEN}}{knife.Identifier} {{DEFAULT}}| {{GOLD}}{knife.Description}"
                );
                index++;
            }
            return ECommandAction.Stopped;
        }

        if (command.ArgCount is not 1)
            return ECommandAction.Stopped;
        
        var targetKnife = command.GetArg(1);
        var knifeIdentifiers = Config.Knives.Select(knife => knife.Identifier).ToArray();

        if (knifeIdentifiers.Contains(targetKnife) is not true)
        {
            deathrunPlayer.SendColoredChatMessage("{RED}Invalid {DEFAULT}knife option!");
            return ECommandAction.Stopped;
        }

        var newKnife = Config.Knives.First(knife => knife.Identifier.Equals(targetKnife, StringComparison.OrdinalIgnoreCase));

        deathrunPlayer.SelectKnife(newKnife);
        
        return ECommandAction.Stopped;
    }
    
    #endregion
    
    #region Knives Config
    
    private static void LoadKnivesConfig()
    {
        if (!Directory.Exists(Knives.ModulePath + "/configs")) 
            Directory.CreateDirectory(Knives.ModulePath + "/configs");
        
        var configPath = Path.Combine(Knives.ModulePath, "configs/knives.json");
        if (!File.Exists(configPath)) CreateKnivesConfig(configPath);

        var config = JsonSerializer.Deserialize<KnivesConfig>(File.ReadAllText(configPath))!;
        Config = config;
    }
    
    private static void CreateKnivesConfig(string configPath)
    {
        var config = new KnivesConfig ();
            
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
    
    public static void ReloadKnivesConfig() { LoadKnivesConfig(); }

    #endregion
    
    int IGameListener.ListenerVersion => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 8;
}

public class KnivesConfig
{
    public string Prefix { get; init; } = "{GREEN}[Knives]{DEFAULT}";
    public List<Knife> Knives { get; init; } = new ()
    {
        new Knife()
        {
            Identifier = "default",
            Name = "Default",
            Description = "Regenerates health every 6 seconds.",
            Value = 3
        },
        new Knife()
        {
            Identifier = "machete",
            Name = "Machete",
            Description = "(x2) Damage | -20% Speed.",
            Value = 2.0f
        },
        new Knife()
        {
            Identifier = "pocket",
            Name = "Pocket",
            Description = "+30% Speed.",
            Value = 1.30f
        },
        new Knife()
        {
            Identifier = "butcher",
            Name = "Butcher",
            Description = "-35% Gravity.",
            Value = 0.65f
        }
    };
}

public class Knife
{
    public string Identifier { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public float Value { get; init; } = 0f;
}




