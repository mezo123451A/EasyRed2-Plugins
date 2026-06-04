using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
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
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class SpottingBehaviour : MonoBehaviour
{
    private const float HoldRepeatSeconds = 0.22f;
    private const float SpotRadius = 80f;

    private float _nextHeldSpot;

    public SpottingBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(KeyCode.Q);
        bool heldRepeat = !pressed && Input.GetKey(KeyCode.Q) && Time.time >= _nextHeldSpot;
        if (!pressed && !heldRepeat)
        {
            return;
        }

        _nextHeldSpot = Time.time + HoldRepeatSeconds;
        TrySpot();
    }

    private bool TrySpot()
    {
        var spotter = GetPlayerSoldier();
        if (spotter == null || IsDead(spotter))
        {
            return false;
        }

        try
        {
            PlayerController.TrySpotInLookDirection();
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Native spot failed: {ex.Message}");
        }

        var camera = Camera.main;
        if (camera != null && TrySpotDirection(camera.transform.position, camera.transform.forward, spotter))
        {
            return true;
        }

        return TrySpotBestVisibleTarget(spotter);
    }

    private static bool TrySpotDirection(Vector3 origin, Vector3 direction, Soldier spotter)
    {
        try
        {
            var target = ResourcesManager.TrySpotSquadInDirection(origin, direction, spotter, SpotRadius);
            if (target == null)
            {
                return false;
            }

            return TrySpotTarget(target, spotter);
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Directional spot failed: {ex.Message}");
            return false;
        }
    }

    private static bool TrySpotBestVisibleTarget(Soldier spotter)
    {
        try
        {
            float distance = 0f;
            var target = spotter.GetBestVisibleEnemy(out distance);
            if (target == null)
            {
                target = spotter.GetCurrentBestVisibleEnemy();
            }

            return target != null && TrySpotTarget(target, spotter);
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Visible-target spot failed: {ex.Message}");
            return false;
        }
    }

    private static bool TrySpotTarget(Spottable target, Soldier spotter)
    {
        bool spotted = false;
        try
        {
            spotted = ResourcesManager.TrySpotSquad(target, spotter);
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

}
