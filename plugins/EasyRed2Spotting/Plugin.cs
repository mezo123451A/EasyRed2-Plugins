using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EasyRed2Spotting;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "tom.easyred2.spotting";
    public const string PluginName = "Easy Red 2 Spotting";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? LogSource { get; private set; }

    public override void Load()
    {
        LogSource = Log;
        ClassInjector.RegisterTypeInIl2Cpp<SpottingBehaviour>();
        AddComponent<SpottingBehaviour>();
        new Harmony(PluginGuid).PatchAll();
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class SpottingBehaviour : MonoBehaviour
{
    private const float TapSpotMaxSeconds = 0.28f;
    private const float CrosshairSpotRadius = 20f;
    private const float ViewSpotRadius = 85f;
    private const float MaxViewSpotDistance = 160f;
    private const float ViewportMargin = 0.04f;

    private bool _spotKeyWasHeld;
    private bool _spotTapCandidate;
    private float _spotKeyDownTime;
    private static bool _allowNativeSpot;

    public SpottingBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        bool held = SpotInputHeld();
        if (held && !_spotKeyWasHeld)
        {
            _spotKeyDownTime = Time.unscaledTime;
            _spotTapCandidate = true;
        }

        if (held && Time.unscaledTime - _spotKeyDownTime > TapSpotMaxSeconds)
        {
            _spotTapCandidate = false;
        }

        if (!held && _spotKeyWasHeld && _spotTapCandidate)
        {
            TryQuickSpot();
        }

        _spotKeyWasHeld = held;
    }

    private bool TryQuickSpot()
    {
        var spotter = GetPlayerSoldier();
        if (spotter == null || IsDead(spotter))
        {
            return false;
        }

        if (TryGetDirectionTarget(spotter, CrosshairSpotRadius, out var crosshairTarget))
        {
            TrySpotTarget(crosshairTarget, spotter);
            TryNativeSpot();
            return true;
        }

        if (TryGetDirectionTarget(spotter, ViewSpotRadius, out var viewTarget)
            || TryGetBestVisibleTargetInView(spotter, out viewTarget))
        {
            return TrySpotTarget(viewTarget, spotter);
        }

        return false;
    }

    internal static bool AllowNativeSpot()
    {
        return _allowNativeSpot;
    }

    private static bool TryGetDirectionTarget(Soldier spotter, float radius, out Spottable target)
    {
        target = null!;
        try
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            target = ResourcesManager.TrySpotSquadInDirection(
                camera.transform.position,
                camera.transform.forward,
                spotter,
                radius);

            return target != null;
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Direction target check failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryGetBestVisibleTargetInView(Soldier spotter, out Spottable target)
    {
        target = null!;
        try
        {
            float distance = 0f;
            target = spotter.GetBestVisibleEnemy(out distance);
            if (target == null)
            {
                target = spotter.GetCurrentBestVisibleEnemy();
            }

            return target != null && TargetIsInCameraView(target);
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Visible target check failed: {ex.Message}");
            return false;
        }
    }

    private static bool TargetIsInCameraView(Spottable target)
    {
        try
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var position = GetTargetSpotPosition(target);
            var viewport = camera.WorldToViewportPoint(position);
            if (viewport.z <= 0f || viewport.z > MaxViewSpotDistance)
            {
                return false;
            }

            return viewport.x >= -ViewportMargin
                && viewport.x <= 1f + ViewportMargin
                && viewport.y >= -ViewportMargin
                && viewport.y <= 1f + ViewportMargin;
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Viewport target check failed: {ex.Message}");
            return false;
        }
    }

    private static Vector3 GetTargetSpotPosition(Spottable target)
    {
        try
        {
            return target.SpotPosition();
        }
        catch
        {
        }

        try
        {
            return target.GetCenterOfUnit();
        }
        catch
        {
            return target.GetPosition();
        }
    }

    private static bool TrySpotTarget(Spottable target, Soldier spotter)
    {
        bool spotted = false;
        try
        {
            if (target.TryCast<Soldier>() is { } soldier)
            {
                spotted = soldier.TrySpot(spotter) || spotted;
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Soldier spot failed: {ex.Message}");
        }

        try
        {
            spotted = ResourcesManager.TrySpotSquad(target, spotter) || spotted;
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Squad spot failed: {ex.Message}");
        }

        try
        {
            spotter.SetBestVisibleEnemy(target);
            spotted = true;
        }
        catch
        {
        }

        return spotted;
    }

    private static void TryNativeSpot()
    {
        _allowNativeSpot = true;
        try
        {
            PlayerController.TrySpotInLookDirection();
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Native spot failed: {ex.Message}");
        }
        finally
        {
            _allowNativeSpot = false;
        }
    }

    private static Soldier? GetPlayerSoldier()
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

    private static bool IsDead(Creature creature)
    {
        try
        {
            return creature.IsDead;
        }
        catch
        {
            return true;
        }
    }

    private static bool SpotInputHeld()
    {
        return KeyHeld(binding => binding.openOrdersMenu, KeyCode.Q) || GamepadButton(GameInput.OrdersMenu);
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

        return fallback != KeyCode.None && Input.GetKey(fallback);
    }

    private static bool GamepadButton(GameInput input)
    {
        try
        {
            var gamepad = GamepadsAPI.GetGamepad();
            return gamepad != null && gamepad.GetButton(input);
        }
        catch
        {
            return false;
        }
    }

}

[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TrySpotInLookDirection))]
internal static class PlayerControllerTrySpotInLookDirectionPatch
{
    private static bool Prefix()
    {
        return SpottingBehaviour.AllowNativeSpot();
    }
}
