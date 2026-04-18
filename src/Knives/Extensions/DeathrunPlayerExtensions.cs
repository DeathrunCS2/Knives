using System.Collections.Generic;
using Deathrun.Knives.Managers;
using DeathrunManager.Shared.DeathrunObjects;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Types;

namespace Deathrun.Knives.Extensions;

public static class DeathrunPlayerExtensions
{
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
    
    public static Knife GetKnife(this IDeathrunPlayer deathrunPlayer)
        => KnivesManager.DeathrunPlayersKnives
                            .GetValueOrDefault(deathrunPlayer.Client.SteamId, KnivesManager.Config.Knives[0]);
    
    public static void ResetKnifeAbilityStates(this IDeathrunPlayer deathrunPlayer)
    {
        if (deathrunPlayer.PlayerPawn?.IsAlive is not true) return;
        deathrunPlayer.PlayerPawn.VelocityModifier = 1;
        deathrunPlayer.PlayerPawn.SetGravityScale(1);
    }

    #endregion
}
