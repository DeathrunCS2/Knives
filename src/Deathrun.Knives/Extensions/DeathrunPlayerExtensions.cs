using System.Collections.Generic;
using Deathrun.Knives.Managers;
using DeathrunManager.Shared.Objects;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Types;

namespace Deathrun.Knives.Extensions;

public static class DeathrunPlayerExtensions
{
    #region Chat
    
    public static void SendColoredAllChatMessage(string message = "")
    {
        if (string.IsNullOrEmpty(message) is true)
        {
            message = "Global message placeholder!";
        }
        
        var coloredPrefix = ProcessColorCodes(KnivesManager.Config.Prefix);
        var coloredMessage = ProcessColorCodes(message);

        var coloredChatMessage = " " + coloredPrefix + " " + coloredMessage;

        Knives.Bridge.ModSharp.PrintChannelFilter(HudPrintChannel.Chat, coloredChatMessage, new RecipientFilter()); 
    }
    
    private static string ProcessColorCodes(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Quick check if the message even contains color codes
        if (!message.Contains('{'))
            return message;

        var result = message;
        foreach (var kvp in ColorCache)
        {
            // Case-insensitive search and replace
            if (result.Contains(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
            {
                result = result.Replace(kvp.Key, kvp.Value, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
    
    private static readonly Dictionary<string, string> ColorCache = new(System.StringComparer.OrdinalIgnoreCase) {
        { "{white}", ChatColor.White },
        { "{default}", ChatColor.White },
        { "{darkred}", ChatColor.DarkRed },
        { "{pink}", ChatColor.Pink },
        { "{green}", ChatColor.Green },
        { "{lightgreen}", ChatColor.LightGreen },
        { "{lime}", ChatColor.Lime },
        { "{red}", ChatColor.Red },
        { "{grey}", ChatColor.Grey },
        { "{gray}", ChatColor.Grey },
        { "{yellow}", ChatColor.Yellow },
        { "{gold}", ChatColor.Gold },
        { "{silver}", ChatColor.Silver },
        { "{blue}", ChatColor.Blue },
        { "{lightblue}", ChatColor.Blue },
        { "{darkblue}", ChatColor.DarkBlue },
        { "{purple}", ChatColor.Purple },
        { "{lightred}", ChatColor.LightRed },
        { "{muted}", ChatColor.Muted },
        { "{head}", ChatColor.Head }
    };
    
    #endregion
    
    #region Knives

    public static void SelectKnife(this IDeathrunPlayer deathrunPlayer, Knife newKnife)
    {
        KnivesManager.DeathrunPlayersKnives.TryRemove(deathrunPlayer.Client.SteamId, out _);
        
        KnivesManager.DeathrunPlayersKnives.TryAdd(deathrunPlayer.Client.SteamId, newKnife);
        
        if (deathrunPlayer.PlayerPawn?.IsAlive is not true) return;
        
        deathrunPlayer.ResetKnifeAbilityStates();

        var activeWeapon = deathrunPlayer.PlayerPawn.GetActiveWeapon();
        if (activeWeapon?.IsValidEntity is not true || activeWeapon.Classname.Contains("knife") is not true) return;
        
        switch (newKnife.Identifier)
        {
            case "pocket":
                deathrunPlayer.PlayerPawn.VelocityModifier = newKnife.Value;
                break;
            case "butcher":
                deathrunPlayer.PlayerPawn.SetGravityScale(newKnife.Value);
                break;
        }
    }
    
    public static Knife? GetKnife(this IDeathrunPlayer deathrunPlayer)
        => KnivesManager.DeathrunPlayersKnives.GetValueOrDefault(deathrunPlayer.Client.SteamId);
    
    public static void ResetKnifeAbilityStates(this IDeathrunPlayer deathrunPlayer)
    {
        if (deathrunPlayer.PlayerPawn?.IsAlive is not true) return;
        deathrunPlayer.PlayerPawn.VelocityModifier = 1;
        deathrunPlayer.PlayerPawn.SetGravityScale(1);
    }

    #endregion
}