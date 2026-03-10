using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deathrun.Knives.Extensions;
using Deathrun.Knives.Interfaces.Managers.SpeedManager;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace Deathrun.Knives.Managers.KnivesManager;

internal class KnivesManager(
    ILogger<KnivesManager> logger,
    ISharedSystem sharedSystem) : IKnivesManager
{
    public static KnivesConfig Config = null!;
    
    public static ConcurrentDictionary<IDeathrunPlayer, Knife> DeathrunPlayersKnives = [];
    
    #region IModule
    
    public bool Init()
    {
        logger.LogInformation("[Deathrun][KnivesManager] {colorMessage}", "Load Speed Manager");
     
        LoadKnivesConfig();
        
        sharedSystem.GetHookManager().PlayerSpawnPost.InstallForward(PlayerSpawnPost);
        sharedSystem.GetHookManager().PlayerRunCommand.InstallHookPre(OnPlayerRunCommandPre);
        sharedSystem.GetHookManager().PlayerDispatchTraceAttack.InstallHookPre(PlayerDispatchTraceAttackPre);
        sharedSystem.GetHookManager().PlayerPostThink.InstallForward(PlayerPostThink);
        sharedSystem.GetHookManager().PlayerGetMaxSpeed.InstallHookPre(OnPlayerGetMaxSpeedPre);
        
        sharedSystem.GetClientManager().InstallCommandCallback("knife", OnClientKnivesCommand);
        sharedSystem.GetClientManager().InstallCommandCallback("knives", OnClientKnivesCommand);
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        sharedSystem.GetHookManager().PlayerSpawnPost.RemoveForward(PlayerSpawnPost);
        sharedSystem.GetHookManager().PlayerRunCommand.RemoveHookPre(OnPlayerRunCommandPre);
        sharedSystem.GetHookManager().PlayerDispatchTraceAttack.RemoveHookPre(PlayerDispatchTraceAttackPre);
        sharedSystem.GetHookManager().PlayerPostThink.RemoveForward(PlayerPostThink);
        sharedSystem.GetHookManager().PlayerGetMaxSpeed.RemoveHookPre(OnPlayerGetMaxSpeedPre);
        
        sharedSystem.GetClientManager().RemoveCommandCallback("knife", OnClientKnivesCommand);
        sharedSystem.GetClientManager().RemoveCommandCallback("knives", OnClientKnivesCommand);
        
        logger.LogInformation("[Deathrun][KnivesManager] {colorMessage}", "Unloaded Speed Manager");
    }

    #endregion

    #region Hooks

    private static void PlayerSpawnPost(IPlayerSpawnForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var client = parms.Client;
            if (client?.IsValid is not true) return;
            
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(client);
            if (deathrunPlayer is null) return;
            
            if (deathrunPlayer.PlayerPawn?.IsValidEntity is not true) return;

            //reset
            deathrunPlayer.PlayerPawn.VelocityModifier = 1;
            deathrunPlayer.PlayerPawn.SetGravityScale(1);
            
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
    }
    
    private static HookReturnValue<EmptyHookReturn> OnPlayerRunCommandPre(IPlayerRunCommandHookParams parms, HookReturnValue<EmptyHookReturn> returnValue)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var client = parms.Client;
            if (client?.IsValid is not true) return default;
            
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(client);
            if (deathrunPlayer is null) return default;

            if (deathrunPlayer.GetKnife()?.Identifier.Equals("butcher", StringComparison.OrdinalIgnoreCase) is true)
            {
                var buttons = parms.KeyButtons;
            
                if (buttons.HasFlag(UserCommandButtons.Jump))
                {
                    if (Math.Abs(deathrunPlayer.PlayerPawn?.GravityScale - deathrunPlayer.GetKnife()?.Value ?? 0) > 1f)
                    {
                        deathrunPlayer.PlayerPawn?.SetGravityScale(deathrunPlayer.GetKnife()?.Value ?? 1);
                    }
                }
            }
            
        }
        
        return default;
    }

    private HookReturnValue<long> PlayerDispatchTraceAttackPre(IPlayerDispatchTraceAttackHookParams parms, HookReturnValue<long> result)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var attackerClient = sharedSystem.GetEntityManager().FindEntityByHandle(parms.AttackerPawnHandle)?.GetController()?.GetGameClient();
            if (attackerClient?.IsValid is not true) return default;
            
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(attackerClient);
            if (deathrunPlayer is null) return default;

            var deathrunPlayerKnife = deathrunPlayer.GetKnife();
            if (deathrunPlayerKnife is null) return default;
            
            if (deathrunPlayerKnife.Identifier.Equals("machete", StringComparison.OrdinalIgnoreCase) is true)
            {
                parms.Damage = (int)(parms.Damage * deathrunPlayerKnife.Value);
                return default;
            }
        }
        
        return default;
    }

    private static void PlayerPostThink(IPlayerThinkForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var client = parms.Client;
            if (client?.IsValid is not true) return;
            
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(client);
            if (deathrunPlayer is null) return;
            
            DeathrunPlayersKnives.TryAdd(deathrunPlayer, Config.Knives[0]);
            
            deathrunPlayer.SetCenterMenuTopRowHtml
            (
                $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#A7A7A7'>Knife: </font>"
                + $"<font class='fontSize-m stratum-font fontWeight-Bold' color='magenta'>{DeathrunPlayersKnives[deathrunPlayer].Name}</font>"    
            );
        }
    }
    
    private static HookReturnValue<float> OnPlayerGetMaxSpeedPre(IPlayerGetMaxSpeedHookParams parms, HookReturnValue<float> original)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(parms.Client);
            if (deathrunPlayer is null) return original;

            if (DeathrunPlayersKnives.TryGetValue(deathrunPlayer, out var deathrunPlayerKnife))
            {
                switch (deathrunPlayerKnife.Identifier)
                {
                    case "machete": 
                        return new HookReturnValue<float>(EHookAction.SkipCallReturnOverride, 0.8f * 250);
                    case "pocket": 
                        return new HookReturnValue<float>(EHookAction.SkipCallReturnOverride, deathrunPlayerKnife.Value * 250);
                }
            }
        }

        return new();
    }

    #endregion
    
    #region Commands
    
    private static ECommandAction OnClientKnivesCommand(IGameClient client, StringCommand command)
    {
        if (Knives.DeathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            var deathrunPlayer = deathrunManagerApi.GetPlayersManager.GetDeathrunPlayer(client);
            if (deathrunPlayer is null) return ECommandAction.Stopped;

            if (command.ArgCount is 0)
            {
                deathrunPlayer.SendColoredChatMessage("{HEAD}Knives List:");
                var index = 1;
                foreach (var knife in Config.Knives)
                {
                    deathrunPlayer.SendColoredChatMessage(
                        $"{{GREEN}}{index}. {{DEFAULT}}{knife.Name} | {{MUTED}}{knife.Description}"
                    );
                    index++;
                }
            }
            else if (command.ArgCount is 1)
            {
                var targetKnife = command.GetArg(1);
                var knifeIdentifiers = Config.Knives.Select(knife => knife.Identifier).ToArray();

                if (knifeIdentifiers.Contains(targetKnife) is not true)
                {
                    deathrunPlayer.SendColoredChatMessage("{RED}Invalid {DEFAULT}knife option!");
                    return ECommandAction.Stopped;
                }

                var knife = Config.Knives.First(knife =>
                    knife.Identifier.Equals(targetKnife, StringComparison.OrdinalIgnoreCase));

                deathrunPlayer.SelectKnife(knife);
            }
        }
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
}

public class KnivesConfig
{
    public string Prefix { get; init; } = "{GREEN}[Knives]{DEFAULT}";
    public List<Knife> Knives { get; init; } = new ()
    {
        new Knife()
        {
            Identifier = "default",
            Name = "Default knife",
            Description = "Health regen over time.",
            Value = 3
        },
        new Knife()
        {
            Identifier = "machete",
            Name = "Machete",
            Description = "More Damage/Low Speed.",
            Value = 2.0f
        },
        new Knife()
        {
            Identifier = "pocket",
            Name = "Pocket knife",
            Description = "High speed.",
            Value = 1.20f
        },
        new Knife()
        {
            Identifier = "butcher",
            Name = "Butcher knife",
            Description = "Low Gravity.",
            Value = 0.7f
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




