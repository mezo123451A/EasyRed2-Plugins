using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EasyRed2ReloadSwitch;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "tom.easyred2.reloadswitch";
    public const string PluginName = "Easy Red 2 Reload Switch";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? LogSource { get; private set; }

    public override void Load()
    {
        LogSource = Log;
        ClassInjector.RegisterTypeInIl2Cpp<ReloadSwitchBehaviour>();
        AddComponent<ReloadSwitchBehaviour>();
        new Harmony(PluginGuid).PatchAll();
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class ReloadSwitchBehaviour : MonoBehaviour
{
    public ReloadSwitchBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        var soldier = ReloadSwitch.PlayerSoldier;
        if (soldier == null)
        {
            return;
        }

        if (!ReloadSwitch.IsReloading(soldier))
        {
            return;
        }

        int numberSlot = ReloadSwitch.GetNumberSlotDown();
        if (numberSlot >= 0)
        {
            ReloadSwitch.ForceSwitchTo(soldier, numberSlot);
            return;
        }

        int direction = ReloadSwitch.GetSwitchDirectionDown();
        if (direction != 0)
        {
            ReloadSwitch.ForceCycleWeapon(soldier, direction);
        }
    }
}

internal static class ReloadSwitch
{
    private const float UnlockSeconds = 0.45f;
    private static IntPtr _unlockSoldier;
    private static float _unlockUntil;
    private static readonly Dictionary<IntPtr, CancelledReload> CancelledReloads = new();

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

    internal static bool IsReloading(Soldier soldier)
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

    internal static bool IsUnlockWindow(Soldier soldier)
    {
        return soldier.Pointer == _unlockSoldier && Time.time <= _unlockUntil;
    }

    internal static void OpenSwitchWindow(Soldier soldier)
    {
        _unlockSoldier = soldier.Pointer;
        _unlockUntil = Time.time + UnlockSeconds;
    }

    internal static void InterruptReload(Soldier soldier)
    {
        GenericGun? gun = null;
        try
        {
            gun = soldier.GetHeldGun();
            if (gun != null)
            {
                CancelReload(gun);
            }
        }
        catch
        {
        }

        try
        {
            soldier.TryInterruptReload();
        }
        catch
        {
        }

        try
        {
            gun?.TryInterruptReload();
        }
        catch
        {
        }

        ForceStopReloadState(soldier);
    }

    internal static void CancelReload(GenericGun gun)
    {
        try
        {
            var snapshot = new CancelledReload(gun);
            CancelledReloads[gun.Pointer] = snapshot;
            gun.TryInterruptReload();
            gun._IsReloading_k__BackingField = false;
            snapshot.Restore();
        }
        catch
        {
        }
    }

    internal static bool IsReloadCancelled(GenericGun? gun)
    {
        return gun != null && CancelledReloads.ContainsKey(gun.Pointer);
    }

    internal static bool KillReloadCoroutine(GenericGun? gun, Soldier? soldier)
    {
        if (gun == null || !CancelledReloads.TryGetValue(gun.Pointer, out var snapshot))
        {
            return false;
        }

        if (soldier != null)
        {
            snapshot.RefreshLiveReferences(soldier);
        }

        snapshot.Restore();
        ForceStopReloadState(gun, soldier);
        ClearCancelledReload(gun);
        return true;
    }

    internal static bool ShouldBlockAmmoIncrease(GenericGun gun, int requestedAmmo)
    {
        if (!CancelledReloads.TryGetValue(gun.Pointer, out var snapshot))
        {
            return false;
        }

        if (requestedAmmo <= snapshot.GunAmmo)
        {
            snapshot.GunAmmo = Math.Max(0, requestedAmmo);
            return false;
        }

        snapshot.Restore();
        return true;
    }

    internal static void ClearCancelledReload(GenericGun gun)
    {
        CancelledReloads.Remove(gun.Pointer);
    }

    internal static void ClearCancelledReload(Soldier? soldier)
    {
        try
        {
            var gun = soldier?.GetHeldGun();
            if (gun != null)
            {
                ClearCancelledReload(gun);
            }
        }
        catch
        {
        }
    }

    internal static void ForceSwitchTo(Soldier soldier, int slot)
    {
        if (slot <= 0 || !HasHeldItem(soldier, slot))
        {
            return;
        }

        OpenSwitchWindow(soldier);
        InterruptReload(soldier);
        TrySwitchTo(soldier, slot);
    }

    internal static void ForceCycleWeapon(Soldier soldier, int direction)
    {
        int current = GetCurrentHeldSlot(soldier);
        int target = FindNextHeldSlot(soldier, current, direction);
        if (target > 0)
        {
            ForceSwitchTo(soldier, target);
        }
    }

    private static void TrySwitchTo(Soldier soldier, int slot)
    {
        try
        {
            soldier.SwitchTo(slot);
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Forced weapon switch failed: {ex.Message}");
        }
    }

    internal static int GetSwitchDirectionDown()
    {
        try
        {
            var binding = GamepadsAPI.keyboardBinding;
            if (binding != null && Input.GetKeyDown(binding.switchGun))
            {
                return 1;
            }
        }
        catch
        {
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            return 1;
        }

        try
        {
            var gamepad = GamepadsAPI.GetGamepad();
            if (gamepad != null)
            {
                if (gamepad.GetButtonDown(GameInput.ChangeWeaponDown))
                {
                    return -1;
                }

                if (gamepad.GetButtonDown(GameInput.SwitchWeapon) || gamepad.GetButtonDown(GameInput.ChangeWeaponUp))
                {
                    return 1;
                }
            }
        }
        catch
        {
        }

        return 0;
    }

    internal static int GetNumberSlotDown()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            return 1;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            return 2;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            return 3;
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            return 4;
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            return 5;
        }

        return -1;
    }

    private static int GetCurrentHeldSlot(Soldier soldier)
    {
        try
        {
            var held = soldier.GetHeldItem_inventory();
            if (held != null)
            {
                int index = soldier.GetHeldItemIndex(held);
                if (index > 0)
                {
                    return index;
                }
            }
        }
        catch
        {
        }

        for (int i = 1; i <= 8; i++)
        {
            try
            {
                var heldItem = soldier.GetHeldItem(i);
                var current = soldier.GetHeldItem();
                if (heldItem != null && current != null && heldItem.Pointer == current.Pointer)
                {
                    return i;
                }
            }
            catch
            {
            }
        }

        return 1;
    }

    private static int FindNextHeldSlot(Soldier soldier, int current, int direction)
    {
        direction = direction < 0 ? -1 : 1;
        const int maxSlots = 8;

        for (int step = 1; step <= maxSlots; step++)
        {
            int slot = current + direction * step;
            while (slot < 1)
            {
                slot += maxSlots;
            }

            while (slot > maxSlots)
            {
                slot -= maxSlots;
            }

            if (slot != current && HasHeldItem(soldier, slot))
            {
                return slot;
            }
        }

        return -1;
    }

    private static bool HasHeldItem(Soldier soldier, int slot)
    {
        try
        {
            return soldier.HasHeldItem(slot) && soldier.GetHeldItem(slot) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void ForceStopReloadState(Soldier soldier)
    {
        try
        {
            soldier._IsReloading_k__BackingField = false;
        }
        catch
        {
        }

        try
        {
            soldier.stopHandsAnim_end = 0f;
        }
        catch
        {
        }

        try
        {
            var gun = soldier.GetHeldGun();
            if (gun != null)
            {
                gun._IsReloading_k__BackingField = false;
            }
        }
        catch
        {
        }
    }

    private static void ForceStopReloadState(GenericGun gun, Soldier? soldier)
    {
        try
        {
            gun.TryInterruptReload();
        }
        catch
        {
        }

        try
        {
            gun._IsReloading_k__BackingField = false;
        }
        catch
        {
        }

        if (soldier == null)
        {
            return;
        }

        try
        {
            soldier.TryInterruptReload();
        }
        catch
        {
        }

        try
        {
            soldier._IsReloading_k__BackingField = false;
            soldier.stopHandsAnim_end = 0f;
        }
        catch
        {
        }
    }

    private sealed class CancelledReload
    {
        private readonly IntPtr _gunPointer;
        private readonly IntPtr _weaponPointer;
        private readonly IntPtr _magazinePointer;
        private GenericGun? _gun;
        private VirtualGunWeapon? _weapon;
        private VirtualMagazineItem? _magazine;

        internal int GunAmmo;
        private readonly int _chamberedAmmo;
        private readonly int _magazineAmmo;

        internal CancelledReload(GenericGun gun)
        {
            _gun = gun;
            _gunPointer = gun.Pointer;
            GunAmmo = SafeGunAmmo(gun);

            var soldier = PlayerSoldier;
            _weapon = SafeHeldWeapon(soldier);
            _weaponPointer = _weapon?.Pointer ?? IntPtr.Zero;
            _chamberedAmmo = _weapon != null ? Math.Max(0, _weapon.chamberedAmmo) : GunAmmo;

            _magazine = SafeInstalledMagazine(_weapon);
            _magazinePointer = _magazine?.Pointer ?? IntPtr.Zero;
            _magazineAmmo = _magazine != null ? Math.Max(0, _magazine.GetAmmoCount()) : 0;
        }

        internal void RefreshLiveReferences(Soldier soldier)
        {
            if (_gun == null || _gun.Pointer != _gunPointer)
            {
                _gun = FindGunByPointer(soldier, _gunPointer);
            }

            if (_weapon == null || _weapon.Pointer != _weaponPointer)
            {
                _weapon = FindWeaponByPointer(soldier, _weaponPointer);
            }

            if (_magazine == null || _magazine.Pointer != _magazinePointer)
            {
                _magazine = FindMagazineByPointer(soldier, _magazinePointer);
            }
        }

        internal void Restore()
        {
            try
            {
                if (_gun != null)
                {
                    if (_gun.GetCurrentAmmoCount() != GunAmmo)
                    {
                        _gun.currentAmmo = GunAmmo;
                    }

                    _gun._IsReloading_k__BackingField = false;
                }
            }
            catch
            {
            }

            try
            {
                if (_weapon != null && _weapon.chamberedAmmo != _chamberedAmmo)
                {
                    _weapon.chamberedAmmo = _chamberedAmmo;
                }
            }
            catch
            {
            }

            try
            {
                var magWeapon = _weapon?.TryCast<VirtualMagazineGunWeapon>();
                if (magWeapon != null && _magazine != null && magWeapon.installedMagazine?.Pointer != _magazine.Pointer)
                {
                    magWeapon.installedMagazine = _magazine;
                }
            }
            catch
            {
            }

            try
            {
                if (_magazine != null && _magazine.GetAmmoCount() != _magazineAmmo)
                {
                    _magazine.SetAmmoCount(_magazineAmmo);
                }
            }
            catch
            {
            }
        }

        private static int SafeGunAmmo(GenericGun gun)
        {
            try
            {
                return Math.Max(0, gun.GetCurrentAmmoCount());
            }
            catch
            {
                return 0;
            }
        }

        private static VirtualGunWeapon? SafeHeldWeapon(Soldier? soldier)
        {
            try
            {
                return soldier?.GetHeldItem_inventory();
            }
            catch
            {
                return null;
            }
        }

        private static VirtualMagazineItem? SafeInstalledMagazine(VirtualGunWeapon? weapon)
        {
            try
            {
                return weapon?.TryCast<VirtualMagazineGunWeapon>()?.installedMagazine;
            }
            catch
            {
                return null;
            }
        }

        private static GenericGun? FindGunByPointer(Soldier soldier, IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            for (int i = 0; i <= 8; i++)
            {
                try
                {
                    var gun = soldier.GetHeldGun(i);
                    if (gun != null && gun.Pointer == pointer)
                    {
                        return gun;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static VirtualGunWeapon? FindWeaponByPointer(Soldier soldier, IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            for (int i = 0; i <= 8; i++)
            {
                try
                {
                    var weapon = soldier.GetHeldItem_inventory(i);
                    if (weapon != null && weapon.Pointer == pointer)
                    {
                        return weapon;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static VirtualMagazineItem? FindMagazineByPointer(Soldier soldier, IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var inventory = soldier.inventory;
                var magazines = inventory?.GetItemsOfType<VirtualMagazineItem>();
                if (magazines == null)
                {
                    return null;
                }

                for (int i = 0; i < magazines.Length; i++)
                {
                    var magazine = magazines[i];
                    if (magazine != null && magazine.Pointer == pointer)
                    {
                        return magazine;
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}

[HarmonyPatch(typeof(GenericGun._ReloadCR_d__125), nameof(GenericGun._ReloadCR_d__125.MoveNext))]
internal static class GenericGunReloadCoroutinePatch
{
    private static bool Prefix(GenericGun._ReloadCR_d__125 __instance, ref bool __result)
    {
        if (!ReloadSwitch.KillReloadCoroutine(__instance.__4__this, __instance.soldier))
        {
            return true;
        }

        __instance.__1__state = -2;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(GenericGun._SingleBulletReload_d__124), nameof(GenericGun._SingleBulletReload_d__124.MoveNext))]
internal static class GenericGunSingleBulletReloadCoroutinePatch
{
    private static bool Prefix(GenericGun._SingleBulletReload_d__124 __instance, ref bool __result)
    {
        if (!ReloadSwitch.KillReloadCoroutine(__instance.__4__this, __instance.soldier))
        {
            return true;
        }

        __instance.__1__state = -2;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(GenericGun), nameof(GenericGun.Reload))]
internal static class GenericGunReloadPatch
{
    private static void Prefix(GenericGun __instance)
    {
        ReloadSwitch.ClearCancelledReload(__instance);
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.Reload))]
internal static class SoldierReloadPatch
{
    private static void Prefix(Soldier __instance)
    {
        ReloadSwitch.ClearCancelledReload(__instance);
    }
}

[HarmonyPatch(typeof(GenericGun), nameof(GenericGun.SetAmmoCount))]
internal static class GenericGunSetAmmoCountPatch
{
    private static bool Prefix(GenericGun __instance, int ammo_count)
    {
        return !ReloadSwitch.ShouldBlockAmmoIncrease(__instance, ammo_count);
    }
}

[HarmonyPatch(typeof(GenericGun), "MoveAmmosFromMagazineToWeapon")]
internal static class GenericGunMoveAmmosFromMagazineToWeaponPatch
{
    private static bool Prefix(GenericGun __instance)
    {
        return !ReloadSwitch.IsReloadCancelled(__instance);
    }
}

[HarmonyPatch(typeof(Soldier), "get_CanUseHandItem")]
internal static class SoldierCanUseHandItemPatch
{
    private static bool Prefix(Soldier __instance, ref bool __result)
    {
        if (!ReloadSwitch.IsUnlockWindow(__instance))
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Soldier), "get_IsPlayingHandAnims")]
internal static class SoldierIsPlayingHandAnimsPatch
{
    private static bool Prefix(Soldier __instance, ref bool __result)
    {
        if (!ReloadSwitch.IsUnlockWindow(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Soldier), nameof(Soldier.SwitchTo))]
internal static class SoldierSwitchToPatch
{
    private static void Prefix(Soldier __instance)
    {
        if (!ReloadSwitch.IsReloading(__instance))
        {
            return;
        }

        ReloadSwitch.OpenSwitchWindow(__instance);
        ReloadSwitch.InterruptReload(__instance);
    }
}
