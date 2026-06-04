using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EasyRed2InfiniteStamina;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "tom.easyred2.infinitestamina";
    public const string PluginName = "Easy Red 2 Infinite Stamina";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? LogSource { get; private set; }

    public override void Load()
    {
        LogSource = Log;
        ClassInjector.RegisterTypeInIl2Cpp<InfiniteStaminaBehaviour>();
        AddComponent<InfiniteStaminaBehaviour>();
        new Harmony(PluginGuid).PatchAll();
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class InfiniteStaminaBehaviour : MonoBehaviour
{
    public InfiniteStaminaBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        var soldier = InfiniteStamina.PlayerSoldier;
        if (soldier != null)
        {
            InfiniteStamina.Refill(soldier);
        }
    }
}

internal static class InfiniteStamina
{
    private const float FullStamina = 100f;
    private const float AirLookMultiplier = 2f;

    internal static Soldier? PlayerSoldier
    {
        get
        {
            try
            {
                return PlayerController.currentController?.ControlledCharacter;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static bool IsPlayerSoldier(Soldier? soldier)
    {
        var player = PlayerSoldier;
        return soldier != null && player != null && soldier.Pointer == player.Pointer;
    }

    internal static void Refill(Soldier soldier)
    {
        try
        {
            soldier.staminaCount = FullStamina;
            soldier.out_of_stamina = false;
        }
        catch
        {
        }
    }

    internal static void RestoreAirLookSpeed(Soldier soldier, ref Vector3 rotation)
    {
        if (!IsPlayerSoldier(soldier) || IsGrounded(soldier))
        {
            return;
        }

        rotation *= AirLookMultiplier;
    }

    internal static void AllowSprintWhileReloading(Soldier soldier, ref bool sprint)
    {
        if (!IsPlayerSoldier(soldier) || !IsReloading(soldier) || !ForwardHeld() || !SprintHeld())
        {
            return;
        }

        sprint = true;
        soldier.isSprinting = true;
        Refill(soldier);
    }

    private static bool IsReloading(Soldier soldier)
    {
        try
        {
            return soldier.IsReloading;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGrounded(Soldier soldier)
    {
        try
        {
            return soldier.IsGrounded;
        }
        catch
        {
            return true;
        }
    }

    private static bool ForwardHeld()
    {
        return KeyHeld(binding => binding.forward, KeyCode.W);
    }

    private static bool SprintHeld()
    {
        return KeyHeld(binding => binding.run, KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private static bool KeyHeld(Func<KeyboardBinding, KeyCode> readBinding, KeyCode fallback)
    {
        try
        {
            var binding = GamepadsAPI.keyboardBinding;
            if (binding != null && Input.GetKey(readBinding(binding)))
            {
                return true;
            }
        }
        catch
        {
        }

        return Input.GetKey(fallback);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.DecreaseStamina))]
internal static class SoldierDecreaseStaminaPatch
{
    private static bool Prefix(Soldier __instance)
    {
        if (!InfiniteStamina.IsPlayerSoldier(__instance))
        {
            return true;
        }

        InfiniteStamina.Refill(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.IsOutOfStamina))]
internal static class SoldierIsOutOfStaminaPatch
{
    private static bool Prefix(Soldier __instance, ref bool __result)
    {
        if (!InfiniteStamina.IsPlayerSoldier(__instance))
        {
            return true;
        }

        InfiniteStamina.Refill(__instance);
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.RotateFPS))]
internal static class SoldierRotateFpsPatch
{
    private static void Prefix(Soldier __instance, ref Vector3 rotation)
    {
        InfiniteStamina.RestoreAirLookSpeed(__instance, ref rotation);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.MoveFPS))]
internal static class SoldierMoveFpsPatch
{
    private static void Prefix(Soldier __instance, ref bool sprint)
    {
        InfiniteStamina.AllowSprintWhileReloading(__instance, ref sprint);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.Move))]
internal static class SoldierMovePatch
{
    private static void Prefix(Soldier __instance, ref bool sprint)
    {
        InfiniteStamina.AllowSprintWhileReloading(__instance, ref sprint);
    }
}
