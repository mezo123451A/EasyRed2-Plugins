using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EasyRed2HealthTweaks;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "tom.easyred2.healthtweaks";
    public const string PluginName = "Easy Red 2 Health Tweaks";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? LogSource { get; private set; }

    public override void Load()
    {
        LogSource = Log;
        ClassInjector.RegisterTypeInIl2Cpp<HealthTweaksBehaviour>();
        AddComponent<HealthTweaksBehaviour>();
        new Harmony(PluginGuid).PatchAll();
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class HealthTweaksBehaviour : MonoBehaviour
{
    private GUIStyle? _labelStyle;
    private GUIStyle? _messageStyle;
    private GUIStyle? _markerStyle;
    private float _nextScan;
    private float _nextAiScan;

    public HealthTweaksBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        HealthTweaks.HideBleedingUi();

        var player = HealthTweaks.PlayerSoldier;
        if (player != null && !HealthTweaks.IsDead(player))
        {
            HealthTweaks.UpdatePlayerReviveInput(player);
        }
        else
        {
            HealthTweaks.CancelPlayerReviveCharge();
        }

        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 0.2f;
            HealthTweaks.UpdateAllSoldiers(0.2f);
        }

        if (Time.time >= _nextAiScan)
        {
            _nextAiScan = Time.time + 1f;
            HealthTweaks.UpdateAiRevives();
        }
    }

    private void OnGUI()
    {
        EnsureStyle();
        DrawReviveMarkers();
        DrawReviveStatus();
        DrawChargeProgress();

        var soldier = HealthTweaks.PlayerSoldier;
        if (soldier == null || HealthTweaks.IsDead(soldier))
        {
            return;
        }

        int health = HealthTweaks.GetHealth(soldier);
        float fill = Mathf.Clamp01(health / 100f);
        const float width = 156f;
        const float height = 9f;
        float x = 26f;
        float y = Screen.height - 92f;

        var oldColor = GUI.color;
        GUI.color = new Color(0.015f, 0.014f, 0.012f, 0.42f);
        GUI.DrawTexture(new Rect(x - 7f, y - 16f, width + 14f, height + 31f), Texture2D.whiteTexture);
        GUI.color = new Color(0.70f, 0.70f, 0.62f, 0.36f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), Texture2D.whiteTexture);
        GUI.color = new Color(0.025f, 0.026f, 0.022f, 0.88f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = health <= 25 ? new Color(0.68f, 0.12f, 0.08f, 0.88f) : new Color(0.78f, 0.77f, 0.66f, 0.86f);
        GUI.DrawTexture(new Rect(x, y, width * fill, height), Texture2D.whiteTexture);
        GUI.color = new Color(0.92f, 0.88f, 0.70f, 0.42f);
        GUI.DrawTexture(new Rect(x, y + height + 3f, width, 1f), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Label(new Rect(x, y - 18f, width, 16f), $"HEALTH {health}", _labelStyle);
    }

    private void DrawReviveStatus()
    {
        if (HealthTweaks.IsPlayerChargingRevive)
        {
            return;
        }

        bool coolingDown = HealthTweaks.PlayerSyringeCooldownRemaining > 0f;
        if (!coolingDown && Time.time >= HealthTweaks.ReviveMessageUntil)
        {
            return;
        }

        string label = coolingDown
            ? $"SYRINGE {HealthTweaks.PlayerSyringeCooldownRemaining:0.0}s"
            : HealthTweaks.ReviveMessage;
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        float fill = coolingDown ? HealthTweaks.PlayerSyringeCooldownFraction : 0f;
        float x = 26f;
        float y = Screen.height - 139f;
        var oldColor = GUI.color;
        GUI.color = new Color(0.015f, 0.014f, 0.012f, 0.46f);
        GUI.DrawTexture(new Rect(x - 7f, y - 5f, 170f, 31f), Texture2D.whiteTexture);
        GUI.color = new Color(0.70f, 0.70f, 0.62f, 0.34f);
        GUI.DrawTexture(new Rect(x - 1f, y + 19f, 158f, 1f), Texture2D.whiteTexture);
        if (coolingDown)
        {
            GUI.color = new Color(0.025f, 0.026f, 0.022f, 0.88f);
            GUI.DrawTexture(new Rect(x, y + 22f, 156f, 4f), Texture2D.whiteTexture);
            GUI.color = new Color(0.46f, 0.45f, 0.38f, 0.78f);
            GUI.DrawTexture(new Rect(x, y + 22f, 156f * fill, 4f), Texture2D.whiteTexture);
        }
        GUI.color = oldColor;

        GUI.Label(new Rect(x, y, 156f, 18f), label, _messageStyle);
    }

    private void DrawChargeProgress()
    {
        if (!HealthTweaks.IsPlayerChargingRevive)
        {
            return;
        }

        const float width = 132f;
        const float height = 6f;
        float x = Screen.width * 0.5f - width * 0.5f;
        float y = Screen.height * 0.58f;
        float fill = HealthTweaks.PlayerChargeFraction;

        var oldColor = GUI.color;
        GUI.color = new Color(0.015f, 0.014f, 0.012f, 0.62f);
        GUI.DrawTexture(new Rect(x - 7f, y - 17f, width + 14f, 31f), Texture2D.whiteTexture);
        GUI.color = new Color(0.025f, 0.026f, 0.022f, 0.92f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = new Color(0.88f, 0.84f, 0.66f, 0.94f);
        GUI.DrawTexture(new Rect(x, y, width * fill, height), Texture2D.whiteTexture);
        GUI.color = new Color(0.70f, 0.70f, 0.62f, 0.42f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1f, y + height, width + 2f, 1f), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Label(new Rect(x, y - 17f, width, 14f), $"REVIVE {HealthTweaks.GetPlayerChargeHealth()} HP", _markerStyle);
    }

    private void DrawReviveMarkers()
    {
        var player = HealthTweaks.PlayerSoldier;
        var camera = Camera.main;
        if (player == null || camera == null || HealthTweaks.IsDead(player))
        {
            return;
        }

        foreach (var target in HealthTweaks.GetMarkerTargets(player))
        {
            Vector3 markerPos = HealthTweaks.GetMarkerPosition(target);
            Vector3 screen = camera.WorldToScreenPoint(markerPos);
            if (screen.z <= 0f)
            {
                continue;
            }

            float x = screen.x;
            float y = Screen.height - screen.y;
            float distance = HealthTweaks.GetReviveDistance(player, target);
            float size = Mathf.Clamp(28f - distance * 0.22f, 15f, 26f);
            float ringFraction = HealthTweaks.GetReviveTimeFraction(target);

            var oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.014f, 0.012f, 0.62f);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.82f, 0.64f, 0.82f);
            GUI.DrawTexture(new Rect(x - 1f, y - size * 0.32f, 2f, size * 0.64f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - size * 0.32f, y - 1f, size * 0.64f, 2f), Texture2D.whiteTexture);
            GUI.color = new Color(0.70f, 0.70f, 0.62f, 0.42f);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y + size * 0.5f, size, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, 1f, size), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + size * 0.5f, y - size * 0.5f, 1f, size), Texture2D.whiteTexture);
            GUI.color = oldColor;

            DrawCircle(new Vector2(x, y), size * 0.72f, 1f, new Color(0.015f, 0.014f, 0.012f, 0.68f), 1.5f);
            DrawCircle(new Vector2(x, y), size * 0.72f, ringFraction, new Color(0.88f, 0.84f, 0.66f, 0.92f), 1.6f);

            if (distance <= HealthTweaks.PlayerReviveRange + 1.5f)
            {
                string label = HealthTweaks.PlayerSyringeCooldownRemaining > 0f ? "COOLDOWN" : "HOLD E";
                GUI.Label(new Rect(x - 48f, y + size * 0.5f + 3f, 96f, 18f), label, _markerStyle);
            }
        }
    }

    private static void DrawCircle(Vector2 center, float radius, float fraction, Color color, float thickness)
    {
        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0f)
        {
            return;
        }

        int segments = Mathf.Clamp(Mathf.CeilToInt(48f * fraction), 4, 48);
        float sweep = 360f * fraction;
        Vector2 previous = PointOnCircle(center, radius, -90f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = -90f + sweep * (i / (float)segments);
            Vector2 next = PointOnCircle(center, radius, angle);
            DrawLine(previous, next, color, thickness);
            previous = next;
        }
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(center.x + Mathf.Cos(radians) * radius, center.y + Mathf.Sin(radians) * radius);
    }

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
        {
            return;
        }

        var oldColor = GUI.color;
        var oldMatrix = GUI.matrix;
        GUI.color = color;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void EnsureStyle()
    {
        if (_labelStyle != null)
        {
            return;
        }

        _labelStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        _labelStyle.normal.textColor = new Color(0.86f, 0.84f, 0.76f, 0.96f);

        _messageStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        _messageStyle.normal.textColor = new Color(0.88f, 0.84f, 0.66f, 0.96f);

        _markerStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        _markerStyle.normal.textColor = new Color(0.88f, 0.84f, 0.66f, 0.92f);
    }
}

internal static class HealthTweaks
{
    private const int MaxHealth = 100;
    private const int MinReviveHealth = 20;
    private const KeyCode ReviveKey = KeyCode.E;
    private const float RegenDelaySeconds = 5.5f;
    private const float RegenPerSecond = 22f;
    internal const float PlayerReviveRange = 5.5f;
    private const float ReviveWindowSeconds = 30f;
    private const float SyringeChargeSeconds = 2f;
    private const float SyringeCooldownSeconds = 4.5f;
    private const float AiSyringeChargeSeconds = 1.75f;
    private const float ReviveMarkerRange = 42f;
    private const float AiReviveRange = 3.25f;
    private const float AiSearchRange = 32f;

    private static readonly Dictionary<IntPtr, TrackedHealth> Tracked = new();
    private static readonly List<Soldier> CachedSoldiers = new();
    private static readonly Dictionary<IntPtr, AiReviveOrder> AiReviveOrders = new();
    private static readonly HashSet<IntPtr> ReviveLocks = new();
    private static readonly Dictionary<IntPtr, ReviveTiming> ReviveTimings = new();
    private static readonly Dictionary<IntPtr, float> SyringeCooldowns = new();

    internal static string ReviveMessage { get; private set; } = string.Empty;
    internal static float ReviveMessageUntil { get; private set; }
    internal static bool IsPlayerChargingRevive { get; private set; }
    internal static float PlayerChargeStartTime { get; private set; }
    internal static Soldier? PlayerChargeTarget { get; private set; }
    internal static float PlayerSyringeCooldownUntil { get; private set; }

    internal static float PlayerChargeFraction => IsPlayerChargingRevive
        ? Mathf.Clamp01((Time.time - PlayerChargeStartTime) / SyringeChargeSeconds)
        : 0f;

    internal static float PlayerSyringeCooldownRemaining => Mathf.Max(0f, PlayerSyringeCooldownUntil - Time.time);

    internal static float PlayerSyringeCooldownFraction => Mathf.Clamp01(1f - PlayerSyringeCooldownRemaining / SyringeCooldownSeconds);

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

    internal static void ShowReviveMessage(string message)
    {
        ReviveMessage = message;
        ReviveMessageUntil = Time.time + 1.8f;
    }

    internal static void UpdatePlayerReviveInput(Soldier player)
    {
        if (IsPlayerChargingRevive)
        {
            if (PlayerChargeTarget == null || !IsRevivableDead(PlayerChargeTarget))
            {
                CancelPlayerReviveCharge();
                return;
            }

            if (!Input.GetKey(ReviveKey))
            {
                int reviveHealth = GetPlayerChargeHealth();
                if (TryReviveSoldier(PlayerChargeTarget, player, reviveHealth))
                {
                    PlayerSyringeCooldownUntil = Time.time + SyringeCooldownSeconds;
                    ShowReviveMessage($"REVIVED {reviveHealth} HP");
                }

                CancelPlayerReviveCharge();
            }

            return;
        }

        if (!Input.GetKeyDown(ReviveKey))
        {
            return;
        }

        if (Time.time < PlayerSyringeCooldownUntil)
        {
            ShowReviveMessage("SYRINGE RECHARGING");
            return;
        }

        var target = FindPlayerReviveTarget(player);
        if (target == null)
        {
            return;
        }

        PlayerChargeTarget = target;
        PlayerChargeStartTime = Time.time;
        IsPlayerChargingRevive = true;
    }

    internal static void CancelPlayerReviveCharge()
    {
        IsPlayerChargingRevive = false;
        PlayerChargeTarget = null;
        PlayerChargeStartTime = 0f;
    }

    internal static int GetPlayerChargeHealth()
    {
        float fraction = PlayerChargeFraction;
        if (fraction >= 1f)
        {
            return MaxHealth;
        }

        int tier = Mathf.FloorToInt(Mathf.Clamp01(fraction) * 4f);
        return Mathf.Clamp(MinReviveHealth + tier * 20, MinReviveHealth, 80);
    }

    internal static void UpdateAllSoldiers(float deltaTime)
    {
        try
        {
            var soldiers = UnityEngine.Object.FindObjectsOfType<Soldier>(true);
            if (soldiers == null)
            {
                return;
            }

            CachedSoldiers.Clear();
            for (int i = 0; i < soldiers.Length; i++)
            {
                var soldier = soldiers[i];
                if (soldier != null)
                {
                    CachedSoldiers.Add(soldier);
                    UpdateSoldier(soldier, deltaTime);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Health scan failed: {ex.Message}");
        }
    }

    internal static void UpdateSoldier(Soldier soldier, float deltaTime)
    {
        if (IsDead(soldier))
        {
            Tracked.Remove(soldier.Pointer);
            RegisterDeath(soldier);
            return;
        }

        ReviveTimings.Remove(soldier.Pointer);
        GrantInfiniteSyringe(soldier);
        ClearCombatStates(soldier);

        int health = GetHealth(soldier);
        var state = GetState(soldier.Pointer, health);
        if (health < state.LastHealth)
        {
            state.LastDamageTime = Time.time;
        }

        if (health > 0 && health < MaxHealth && Time.time - state.LastDamageTime >= RegenDelaySeconds)
        {
            int nextHealth = Math.Min(MaxHealth, health + Math.Max(1, Mathf.CeilToInt(RegenPerSecond * deltaTime)));
            SetHealth(soldier, nextHealth);
            health = nextHealth;
        }

        state.LastHealth = health;
    }

    internal static Soldier? FindPlayerReviveTarget(Soldier player)
    {
        Camera? camera = Camera.main;
        Soldier? best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < CachedSoldiers.Count; i++)
        {
            var target = CachedSoldiers[i];
            if (!CanReviveTarget(player, target))
            {
                continue;
            }

            Vector3 bodyPosition = GetReviveBodyPosition(target);
            float distance = Vector3.Distance(player.transform.position, bodyPosition);
            if (distance > PlayerReviveRange)
            {
                continue;
            }

            float score = distance;
            if (camera != null)
            {
                Vector3 viewport = camera.WorldToViewportPoint(GetReviveAimPosition(target));
                if (viewport.z <= 0f)
                {
                    continue;
                }

                float aimDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
                if (aimDistance > 0.18f)
                {
                    continue;
                }

                score = aimDistance * 100f + distance;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }

        return best;
    }

    internal static IEnumerable<Soldier> GetMarkerTargets(Soldier player)
    {
        for (int i = 0; i < CachedSoldiers.Count; i++)
        {
            var target = CachedSoldiers[i];
            if (!CanReviveTarget(player, target))
            {
                continue;
            }

            if (GetReviveDistance(player, target) <= ReviveMarkerRange)
            {
                yield return target;
            }
        }
    }

    internal static Vector3 GetMarkerPosition(Soldier soldier)
    {
        return GetReviveBodyPosition(soldier) + Vector3.up * 1.2f;
    }

    internal static Vector3 GetReviveAimPosition(Soldier soldier)
    {
        return GetReviveBodyPosition(soldier) + Vector3.up * 0.35f;
    }

    internal static float GetReviveDistance(Soldier reviver, Soldier target)
    {
        return Vector3.Distance(reviver.transform.position, GetReviveBodyPosition(target));
    }

    internal static Vector3 GetReviveBodyPosition(Soldier soldier)
    {
        if (TryGetRagdollCenter(soldier, out var ragdollCenter))
        {
            return ragdollCenter;
        }

        if (TryGetBoneCenter(soldier, out var boneCenter))
        {
            return boneCenter;
        }

        try
        {
            return soldier.GetCenterOfUnit();
        }
        catch
        {
            return soldier.transform.position;
        }
    }

    private static bool TryGetRagdollCenter(Soldier soldier, out Vector3 center)
    {
        center = Vector3.zero;
        try
        {
            var ragdoll = soldier.ragdoll_manager;
            var bones = ragdoll?.rigidbodyToAddList;
            if (ragdoll == null || bones == null || bones.Count == 0)
            {
                return false;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < bones.Count; i++)
            {
                AddBonePosition(bones[i], ref sum, ref count);
            }

            if (count <= 0)
            {
                return false;
            }

            center = sum / count;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetBoneCenter(Soldier soldier, out Vector3 center)
    {
        center = Vector3.zero;
        try
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            AddBonePosition(soldier.spineRotPos, ref sum, ref count);
            AddBonePosition(soldier.neckRotPos, ref sum, ref count);
            AddBonePosition(soldier.leftUpLeg, ref sum, ref count);
            AddBonePosition(soldier.rightUpLeg, ref sum, ref count);

            if (count <= 0)
            {
                return false;
            }

            center = sum / count;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddBonePosition(Transform? bone, ref Vector3 sum, ref int count)
    {
        if (bone == null)
        {
            return;
        }

        Vector3 position = bone.position;
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
        {
            return;
        }

        sum += position;
        count++;
    }

    internal static float GetReviveTimeFraction(Soldier soldier)
    {
        var timing = RegisterDeath(soldier);
        if (timing == null)
        {
            return 0f;
        }

        return Mathf.Clamp01((timing.Deadline - Time.time) / ReviveWindowSeconds);
    }

    internal static void UpdateAiRevives()
    {
        try
        {
            CleanupAiOrders();

            for (int i = 0; i < CachedSoldiers.Count; i++)
            {
                var target = CachedSoldiers[i];
                if (!IsRevivableDead(target))
                {
                    continue;
                }

                Soldier? reviver = GetAssignedReviver(target);
                if (reviver == null)
                {
                    reviver = FindAiReviver(target);
                    if (reviver == null)
                    {
                        continue;
                    }

                    AiReviveOrders[target.Pointer] = new AiReviveOrder(reviver.Pointer);
                }

                if (!AiReviveOrders.TryGetValue(target.Pointer, out var order))
                {
                    order = new AiReviveOrder(reviver.Pointer);
                    AiReviveOrders[target.Pointer] = order;
                }

                float distance = GetReviveDistance(reviver, target);
                if (distance <= AiReviveRange)
                {
                    if (order.ChargeStartTime <= 0f)
                    {
                        order.ChargeStartTime = Time.time;
                    }

                    if (Time.time - order.ChargeStartTime >= AiSyringeChargeSeconds)
                    {
                        if (TryReviveSoldier(target, reviver, MaxHealth))
                        {
                            SyringeCooldowns[reviver.Pointer] = Time.time + SyringeCooldownSeconds;
                        }
                    }

                    continue;
                }

                reviver = ResetAiChargeIfMoving(reviver, target);
                OrderAiTowardBody(reviver, target);
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"AI revive scan failed: {ex.Message}");
        }
    }

    internal static bool TryReviveSoldier(Soldier target, Soldier? reviver, int reviveHealth = MaxHealth)
    {
        if (reviver == null || !CanReviveTarget(reviver, target))
        {
            return false;
        }

        IntPtr pointer = target.Pointer;
        if (ReviveLocks.Contains(pointer))
        {
            return false;
        }

        ReviveLocks.Add(pointer);
        bool wasPlayerControlled = IsPlayerControlledSoldier(target);
        try
        {
            Vector3 revivePosition = GetReviveBodyPosition(target);
            reviveHealth = Mathf.Clamp(reviveHealth, MinReviveHealth, MaxHealth);
            ClearCombatStates(target);
            SetHealth(target, reviveHealth);

            try
            {
                target.SetIncapacitated(false);
            }
            catch
            {
            }

            try
            {
                target.BloodFxValue = 0;
            }
            catch
            {
            }

            try
            {
                target.isCulled = false;
            }
            catch
            {
            }

            try
            {
                target.gameObject.SetActive(true);
                target.transform.position = revivePosition;
            }
            catch
            {
            }

            try
            {
                var ragdoll = target.ragdoll_manager;
                if (ragdoll != null)
                {
                    ragdoll.Deragdolize();
                    ragdoll.ragdollized = false;
                }
                target.transform.position = revivePosition;
            }
            catch
            {
            }

            ResetRevivedSoldierState(target);

            try
            {
                target.RenderUnit(true);
                target.SetVisible(true);
            }
            catch
            {
            }

            try
            {
                target.RefreshControllerEnabled();
                target.ForceControllerEnabled();
                target.RefreshAnimatorState();
            }
            catch
            {
            }

            try
            {
                if (wasPlayerControlled)
                {
                    RestorePlayerControlAfterRevive(target);
                }
                else if (target.aiController == null)
                {
                    target.SetAI();
                }
            }
            catch
            {
            }

            GrantInfiniteSyringe(target);
            AddAliveCreature(target);
            Tracked[target.Pointer] = new TrackedHealth(reviveHealth, Time.time);
            ReviveTimings.Remove(target.Pointer);
            RefundInvaderTicketIfNeeded(target);
            AiReviveOrders.Remove(pointer);
            Plugin.LogSource?.LogInfo($"Revived {SafeSoldierName(target)} with {reviveHealth} HP.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogWarning($"Revive failed: {ex.Message}");
            return false;
        }
        finally
        {
            ReviveLocks.Remove(pointer);
        }
    }

    private static void ResetRevivedSoldierState(Soldier soldier)
    {
        try
        {
            soldier.stopBodyAnim_end = 0f;
            soldier.stopHandsAnim_end = 0f;
            soldier.slowDownMovement_time = 0f;
            soldier.injureDeathTime = 0f;
            soldier.throw_end_time = 0f;
            soldier.surrended = false;
            soldier.doingMelee = false;
            soldier.isIkOn = false;
            soldier.IsReloading = false;
            soldier.IsRendered = true;
        }
        catch
        {
        }

        try
        {
            var controller = soldier.m_controller;
            if (controller != null)
            {
                controller.enabled = true;
            }
        }
        catch
        {
        }

        try
        {
            var animator = soldier.m_Animator;
            if (animator != null)
            {
                animator.enabled = true;
            }
        }
        catch
        {
        }
    }

    private static void RestorePlayerControlAfterRevive(Soldier soldier)
    {
        try
        {
            var controller = PlayerController.currentController;
            if (controller != null)
            {
                controller.SetPlayer(soldier, 0f);
                controller.ControlledCharacter = soldier;
            }
        }
        catch
        {
        }

        try
        {
            soldier.OnSetPlayer(0f);
        }
        catch
        {
        }

        try
        {
            PlayerController.ResetLookRotation();
            PlayerController.RefreshAdaptiveTriggerValue();
        }
        catch
        {
        }
    }

    private static bool IsPlayerControlledSoldier(Soldier soldier)
    {
        try
        {
            var current = PlayerController.currentController?.ControlledCharacter;
            if (current != null && current.Pointer == soldier.Pointer)
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            return soldier.IsPlayer();
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsDead(Creature? creature)
    {
        try
        {
            return creature == null || creature.IsDead;
        }
        catch
        {
            return creature == null;
        }
    }

    internal static bool IsRevivableDead(Soldier? soldier)
    {
        if (soldier == null)
        {
            return false;
        }

        try
        {
            if (soldier.gameObject == null)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        if (!IsDead(soldier))
        {
            ReviveTimings.Remove(soldier.Pointer);
            return false;
        }

        var timing = RegisterDeath(soldier);
        return timing != null && Time.time <= timing.Deadline;
    }

    internal static int GetHealth(Creature creature)
    {
        try
        {
            return Mathf.Clamp((int)creature.life_total, 0, MaxHealth);
        }
        catch
        {
            return 0;
        }
    }

    internal static void SetHealth(Creature creature, int health)
    {
        try
        {
            creature.life_total = Mathf.Clamp(health, 0, MaxHealth);
            creature.UpdateLifeFX();
        }
        catch
        {
        }
    }

    internal static void HealToFull(Creature? creature)
    {
        if (creature == null || IsDead(creature))
        {
            return;
        }

        ClearCombatStates(creature);
        SetHealth(creature, MaxHealth);
        Tracked[creature.Pointer] = new TrackedHealth(MaxHealth, Time.time);
    }

    internal static void ClearCombatStates(Creature creature)
    {
        try
        {
            creature.isBleeding = false;
        }
        catch
        {
        }

        try
        {
            creature.SetBleeding(false);
        }
        catch
        {
        }
    }

    internal static void HideBleedingUi()
    {
        try
        {
            var gui = PlayerGUI.instance;
            if (gui?.bleedingOutUi != null && gui.bleedingOutUi.activeSelf)
            {
                gui.bleedingOutUi.SetActive(false);
            }
        }
        catch
        {
        }
    }

    internal static void MarkNotIncapacitated(Creature creature)
    {
        ClearCombatStates(creature);
        int health = GetHealth(creature);
        if (health <= 0 && !IsDead(creature))
        {
            SetHealth(creature, 1);
        }
    }

    private static void GrantInfiniteSyringe(Soldier soldier)
    {
        try
        {
            soldier.hasSyringe = true;
        }
        catch
        {
        }
    }

    private static void AddAliveCreature(Creature creature)
    {
        try
        {
            var alive = Creature.aliveCreatures;
            if (alive != null && !alive.Contains(creature))
            {
                alive.Add(creature);
            }
        }
        catch
        {
        }
    }

    private static Soldier? GetAssignedReviver(Soldier target)
    {
        if (!AiReviveOrders.TryGetValue(target.Pointer, out var order))
        {
            return null;
        }

        for (int i = 0; i < CachedSoldiers.Count; i++)
        {
            var soldier = CachedSoldiers[i];
            if (soldier.Pointer == order.ReviverPointer && IsValidAiReviver(soldier, target))
            {
                return soldier;
            }
        }

        AiReviveOrders.Remove(target.Pointer);
        return null;
    }

    private static Soldier? FindAiReviver(Soldier target)
    {
        Soldier? best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < CachedSoldiers.Count; i++)
        {
            var candidate = CachedSoldiers[i];
            if (!IsValidAiReviver(candidate, target))
            {
                continue;
            }

            float distance = GetReviveDistance(candidate, target);
            if (distance <= AiSearchRange && distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static bool IsValidAiReviver(Soldier candidate, Soldier target)
    {
        try
        {
            if (candidate == null || IsDead(candidate) || !candidate.IsAI())
            {
                return false;
            }

            if (SyringeCooldowns.TryGetValue(candidate.Pointer, out float cooldownUntil) && Time.time < cooldownUntil)
            {
                return false;
            }

            GrantInfiniteSyringe(candidate);
            return CanReviveTarget(candidate, target);
        }
        catch
        {
            return false;
        }
    }

    internal static bool CanReviveTarget(Soldier reviver, Soldier target)
    {
        try
        {
            return reviver != null
                && target != null
                && reviver.Pointer != target.Pointer
                && IsRevivableDead(target)
                && IsFriendlySoldier(reviver, target);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsFriendlySoldier(Soldier first, Soldier second)
    {
        try
        {
            return first != null && second != null && IsSameFaction(first, second);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSameFaction(Soldier first, Soldier second)
    {
        string firstFaction;
        string secondFaction;

        try
        {
            firstFaction = first.faction;
            secondFaction = second.faction;
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(firstFaction) || string.IsNullOrEmpty(secondFaction))
        {
            return false;
        }

        try
        {
            return Lua_API.isSameFaction(firstFaction, secondFaction);
        }
        catch
        {
        }

        try
        {
            return string.Equals(firstFaction, secondFaction, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void OrderAiTowardBody(Soldier reviver, Soldier target)
    {
        if (!AiReviveOrders.TryGetValue(target.Pointer, out var order))
        {
            order = new AiReviveOrder(reviver.Pointer);
            AiReviveOrders[target.Pointer] = order;
        }

        if (Time.time < order.NextOrderTime)
        {
            return;
        }

        order.NextOrderTime = Time.time + 2.5f;
        try
        {
            var destination = reviver.GetDestinationInPosition(GetReviveBodyPosition(target), 1.75f, null, false);
            if (destination != null)
            {
                reviver.CoverPosition(destination);
                reviver.PrioritizeReachingDestination();
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"AI revive move order failed: {ex.Message}");
        }
    }

    private static Soldier ResetAiChargeIfMoving(Soldier reviver, Soldier target)
    {
        if (AiReviveOrders.TryGetValue(target.Pointer, out var order))
        {
            order.ChargeStartTime = 0f;
        }

        return reviver;
    }

    private static void CleanupAiOrders()
    {
        if (AiReviveOrders.Count == 0)
        {
            return;
        }

        var toRemove = new List<IntPtr>();
        foreach (var pair in AiReviveOrders)
        {
            Soldier? target = null;
            for (int i = 0; i < CachedSoldiers.Count; i++)
            {
                if (CachedSoldiers[i].Pointer == pair.Key)
                {
                    target = CachedSoldiers[i];
                    break;
                }
            }

            if (!IsRevivableDead(target))
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            AiReviveOrders.Remove(toRemove[i]);
        }
    }

    internal static void RegisterDeathFromPatch(Soldier soldier)
    {
        RegisterDeath(soldier);
    }

    private static ReviveTiming? RegisterDeath(Soldier soldier)
    {
        try
        {
            if (soldier == null || !IsDead(soldier))
            {
                return null;
            }

            if (!ReviveTimings.TryGetValue(soldier.Pointer, out var timing))
            {
                timing = new ReviveTiming(Time.time, Time.time + ReviveWindowSeconds);
                ReviveTimings[soldier.Pointer] = timing;
            }

            return timing;
        }
        catch
        {
            return null;
        }
    }

    private static void RefundInvaderTicketIfNeeded(Soldier target)
    {
        try
        {
            string invaderFaction = Lua_API.getInvadersFaction();
            if (!string.IsNullOrEmpty(invaderFaction) && Lua_API.isSameFaction(target.faction, invaderFaction))
            {
                Lua_API.addInvadersTickets(1);
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Ticket refund failed: {ex.Message}");
        }
    }

    private static string SafeSoldierName(Soldier soldier)
    {
        try
        {
            return soldier.name;
        }
        catch
        {
            return "soldier";
        }
    }

    private static TrackedHealth GetState(IntPtr pointer, int health)
    {
        if (!Tracked.TryGetValue(pointer, out var state))
        {
            state = new TrackedHealth(health, Time.time);
            Tracked[pointer] = state;
        }

        return state;
    }

    private sealed class TrackedHealth
    {
        internal int LastHealth;
        internal float LastDamageTime;

        internal TrackedHealth(int health, float lastDamageTime)
        {
            LastHealth = health;
            LastDamageTime = lastDamageTime;
        }
    }

    private sealed class AiReviveOrder
    {
        internal readonly IntPtr ReviverPointer;
        internal float NextOrderTime;
        internal float ChargeStartTime;

        internal AiReviveOrder(IntPtr reviverPointer)
        {
            ReviverPointer = reviverPointer;
        }
    }

    private sealed class ReviveTiming
    {
        internal readonly float DeathTime;
        internal readonly float Deadline;

        internal ReviveTiming(float deathTime, float deadline)
        {
            DeathTime = deathTime;
            Deadline = deadline;
        }
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.SetBleeding))]
internal static class SoldierSetBleedingPatch
{
    private static void Prefix(ref bool bleeding)
    {
        bleeding = false;
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.SetIncapacitated))]
internal static class SoldierSetIncapacitatedPatch
{
    private static void Prefix(Soldier __instance, ref bool incapacitate)
    {
        if (!incapacitate)
        {
            return;
        }

        incapacitate = false;
        HealthTweaks.MarkNotIncapacitated(__instance);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.Kill))]
internal static class SoldierKillPatch
{
    private static void Postfix(Soldier __instance)
    {
        HealthTweaks.RegisterDeathFromPatch(__instance);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.KillSynched))]
internal static class SoldierKillSynchedPatch
{
    private static void Postfix(Soldier __instance)
    {
        HealthTweaks.RegisterDeathFromPatch(__instance);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.UseSyrynge))]
internal static class SoldierUseSyringePatch
{
    private static void Prefix(ref bool force_pass)
    {
        force_pass = true;
    }

    private static void Postfix(Soldier __instance, Creature injuredCreature)
    {
        if (injuredCreature is Soldier soldier)
        {
            if (!HealthTweaks.IsFriendlySoldier(__instance, soldier))
            {
                return;
            }

            if (HealthTweaks.IsRevivableDead(soldier))
            {
                HealthTweaks.TryReviveSoldier(soldier, __instance);
                return;
            }

            HealthTweaks.HealToFull(soldier);
            return;
        }

        HealthTweaks.HealToFull(injuredCreature);
    }
}
