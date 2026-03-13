using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deathrun.Knives.Extensions;
using Deathrun.Knives.Interfaces.Managers.SpeedManager;
using DeathrunManager.Shared.Objects;
using MySqlConnector;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.HookParams;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Dapper;
using DeathrunManager.Shared.Enums;

namespace Deathrun.Knives.Managers.KnivesManager;

internal class KnivesManager(
    IModSharp modSharp,
    IHookManager hookManager,
    IEntityManager entityManager,
    IClientManager clientManager) : IKnivesManager, IGameListener, IClientListener
{
    private static IGlobalVars? _globalVars = null;
    public static KnivesConfig Config = null!;
    private static string ConnectionString { get; set; } = "";

    public static readonly ConcurrentDictionary<IDeathrunPlayer, Knife> DeathrunPlayersKnives = [];
    
    private bool _addCommandAnnouncers = false;
    
    #region IModule
    
    public bool Init()
    {
        LoadKnivesConfig();
        
        hookManager.PlayerSwitchWeapon.InstallForward(PlayerSwitchWeapon);
        hookManager.PlayerEquipWeapon.InstallForward(PlayerEquipWeapon);
        hookManager.PlayerDispatchTraceAttack.InstallHookPre(PlayerDispatchTraceAttackPre);
        hookManager.PlayerPostThink.InstallForward(PlayerPostThink);
        hookManager.PlayerGetMaxSpeed.InstallHookPre(PlayerGetMaxSpeedPre);
        
        clientManager.InstallClientListener(this);
        modSharp.InstallGameListener(this);
        
        clientManager.InstallCommandCallback("knife", OnClientKnivesCommand);
        clientManager.InstallCommandCallback("knives", OnClientKnivesCommand);

        if (Config.SaveKnivesToDatabase is true)
        {
            //build connection string
            BuildDbConnectionString();

            //create the necessary db tables
            SetupDatabaseTables();
        }
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        hookManager.PlayerSwitchWeapon.RemoveForward(PlayerSwitchWeapon);
        hookManager.PlayerEquipWeapon.RemoveForward(PlayerEquipWeapon);
        hookManager.PlayerDispatchTraceAttack.RemoveHookPre(PlayerDispatchTraceAttackPre);
        hookManager.PlayerPostThink.RemoveForward(PlayerPostThink);
        hookManager.PlayerGetMaxSpeed.RemoveHookPre(PlayerGetMaxSpeedPre);
        
        clientManager.RemoveClientListener(this);
        modSharp.RemoveGameListener(this);
        
        clientManager.RemoveCommandCallback("knife", OnClientKnivesCommand);
        clientManager.RemoveCommandCallback("knives", OnClientKnivesCommand);
    }

    #endregion

    #region Hooks
    
    private static void PlayerEquipWeapon(IPlayerEquipWeaponForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var switchedToWeapon = parms.Weapon;
        if (switchedToWeapon?.IsValidEntity is not true) return;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;

        UpdateKnifeAbilityState(deathrunPlayer, switchedToWeapon);
    }
    
    private static void PlayerSwitchWeapon(IPlayerSwitchWeaponForwardParams parms)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        var equippedWeapon = parms.Weapon;
        if (equippedWeapon?.IsValidEntity is not true) return;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;

        UpdateKnifeAbilityState(deathrunPlayer, equippedWeapon);
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
        if (deathrunPlayer?.IsValid is not true) return;
        
        var deathrunPlayerKnife = deathrunPlayer.GetKnife();
        if (deathrunPlayerKnife is null) return;
        
        deathrunPlayer.SetCenterMenuTopRowHtml
        (
            $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#A7A7A7'>Knife: </font>"
            + $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#efbfff'>{deathrunPlayerKnife.Name}</font>"    
        );
        
        //limit to every 6 seconds
        if (_globalVars?.TickCount % 384 is not 0) return;
        
        //skip if the player's knife is not the default type
        if (deathrunPlayerKnife.Identifier.Equals("default") is not true) return;
            
        //skip invalid/dead players
        if (deathrunPlayer.IsValidAndAlive is not true || deathrunPlayer.PlayerPawn is null) return;
        
        if (deathrunPlayer.PlayerPawn.Health >= 100) return;
        
        var activeWeapon = deathrunPlayer.PlayerPawn.GetActiveWeapon();
        if (activeWeapon?.IsValidEntity is not true || activeWeapon.Classname.Contains("knife") is not true) return;

        deathrunPlayer.PlayerPawn.Health += (int) (deathrunPlayerKnife.Value);
        
        //clamp health to 100
        if (deathrunPlayer.PlayerPawn.Health > 100) deathrunPlayer.PlayerPawn.Health = 100;
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
    
    //game listeners
    public void OnServerInit() => _globalVars = modSharp.GetGlobals();
    
    public void OnGameActivate()
    {
        if (_addCommandAnnouncers is true) return;
        
        modSharp.PushTimer(() =>
        {
            DeathrunPlayerExtensions.SendColoredAllChatMessage("You can select a knife by typing {GREEN}/knife {DEFAULT}or {GREEN}/knives {DEFAULT}in the chat!");
        }, Random.Shared.Next(25), GameTimerFlags.StopOnMapEnd);
        
        _addCommandAnnouncers = true;
    }

    public void OnGameDeactivate()
    {
        _addCommandAnnouncers = false;
    }

    //client listeners
    public void OnClientConnected(IGameClient client)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        if (Config.SaveKnivesToDatabase is not true || client.SteamId == 0) return;
        
        //try getting saved knife from the database
        modSharp.PushTimer(() =>
        {
            var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(client);
            if (deathrunPlayer is null) return;
            
            Task.Run(async () =>
            {
                var savedKnifeIdentifier = await GetSavedKnife(deathrunPlayer.Client.SteamId);
                var newKnife = Config.Knives.First(knife => knife.Identifier.Equals(savedKnifeIdentifier, StringComparison.OrdinalIgnoreCase));
                
                deathrunPlayer.SelectKnife(newKnife);
            });
        } ,4f);
    }
    
    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (Knives.DeathrunManagerApi?.Instance is not { } deathrunManagerApi) return;
        
        if (Config.SaveKnivesToDatabase is not true || client.SteamId == 0) return;
        
        var deathrunPlayer = deathrunManagerApi.Managers.PlayersManager.GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return;
        
        Task.Run(async () =>
        {
            var deathrunPlayerKnife = deathrunPlayer.GetKnife();
            if (deathrunPlayerKnife is null) return;
            
            await SaveSelectedKnifeToDatabase(deathrunPlayer.Client.SteamId, deathrunPlayerKnife.Identifier);
            
            DeathrunPlayersKnives.TryRemove(deathrunPlayer, out _);
        });
    }
    
    #endregion
    
    #region State update method/s

    private static void UpdateKnifeAbilityState(IDeathrunPlayer deathrunPlayer, IBaseWeapon currentWeapon)
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
            deathrunPlayer.SendChatMessage("{DEFAULT}Knives{PURPLE}(Special trait applies only when holding the knife){DEFAULT}: ");
            deathrunPlayer.SendChatMessage("{LIGHTBLUE}Example: /knife butcher");
            var index = 1;
            foreach (var knife in Config.Knives)
            {
                deathrunPlayer.SendChatMessage(
                    $"{{GREEN}}{index}. {{GREEN}}{knife.Name} {{DEFAULT}}| {{GOLD}}{knife.Description}"
                );
                index++;
            }
            return ECommandAction.Stopped;
        }

        if (command.ArgCount is not 1)
            return ECommandAction.Stopped;
        
        var targetKnife = command.GetArg(1).ToLower();
        var knifeIdentifiers = Config.Knives.Select(knife => knife.Identifier).ToArray();

        if (knifeIdentifiers.Contains(targetKnife) is not true)
        {
            deathrunPlayer.SendChatMessage("{RED}Invalid {DEFAULT}knife option!");
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
    
    #region Async methods
    
    private static async Task SaveSelectedKnifeToDatabase(ulong steamId64, string currentKnifeIdentifier)
    {
        try
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var insertUpdateKnifeQuery 
                = $@" INSERT INTO `{Config.TableName}` 
                      ( steamid64, `knife` )  
                      VALUES 
                      ( @SteamId64, @NewKnife ) 
                      ON DUPLICATE KEY UPDATE 
                                       `knife`  = '{currentKnifeIdentifier}'
                    ";
    
            await connection.ExecuteAsync(insertUpdateKnifeQuery,
                new {
                    SteamId64        = steamId64, 
                    NewKnife         = currentKnifeIdentifier
                });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
    }
    
    private static async Task<string> GetSavedKnife(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
    
            //fast check if the player has saved lives data
            var hasSavedKnifeData = await HasSavedKnifeData(steamId64);
            if (hasSavedKnifeData is not true) return "default";
            
            //take the lives num from the database
            var savedKnifeIdentifier = await connection.QueryFirstOrDefaultAsync<string>
            ($@"SELECT
                       `knife`
                    FROM `{Config.TableName}`
                    WHERE steamid64 = @SteamId64
                 ",
                new { SteamId64 = steamId64 }
            
            );
            
            return savedKnifeIdentifier ?? "default";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return "default";
    }

    private static async Task<bool> HasSavedKnifeData(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
    
            var hasSavedKnifeData 
                = await connection.QueryFirstOrDefaultAsync<bool>
                                    ($@"SELECT EXISTS(SELECT 1 FROM `{Config.TableName}`
                                            WHERE steamid64 = @SteamId64 LIMIT 1)
                                         ",
                                        new { SteamId64 = steamId64 }
                                    
                                    );
            
            return hasSavedKnifeData;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return false;
    }
    
    #endregion
    
    #region ConnectionString

    private static void BuildDbConnectionString() 
    {
        //build connection string
        ConnectionString = new MySqlConnectionStringBuilder
        {
            Database = Config.Database,
            UserID = Config.User,
            Password = Config.Password,
            Server = Config.Host,
            Port = (uint)Config.Port,
        }.ConnectionString;
    }

    #endregion
    
    #region Tables

    private static void SetupDatabaseTables()
    {
        Task.Run(() => CreateDatabaseTable($@" CREATE TABLE IF NOT EXISTS `{Config.TableName}` 
                                               (
                                                   `id` BIGINT NOT NULL AUTO_INCREMENT,
                                                   `steamid64` BIGINT(255) NOT NULL UNIQUE,
                                                   `knife` VARCHAR(20) DEFAULT 'default',
                                                    
                                                   PRIMARY KEY (id)
                                               )"));
    }
    
    private static async Task CreateDatabaseTable(string databaseTableStringStructure)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(ConnectionString);
            dbConnection.Open();
            
            await dbConnection.ExecuteAsync(databaseTableStringStructure);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    #endregion
    
    int IGameListener.ListenerVersion => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 7;
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 7;
}

public class KnivesConfig
{
    public string Prefix { get; init; } = "{GREEN}[Knives]{DEFAULT}";
    public bool SaveKnivesToDatabase { get; init; } = true;
    public string Host { get; init; } = "localhost";
    public string Database { get; init; } = "database_name";
    public string User { get; init; } = "database_user";
    public string Password { get; init; } = "database_password";
    public int Port { get; init; } = 3306;
    public string TableName { get; init; } = "deathrun_knives";
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




