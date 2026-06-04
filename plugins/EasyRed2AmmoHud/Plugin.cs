using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EasyRed2AmmoHud;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "tom.easyred2.ammohud";
    public const string PluginName = "Easy Red 2 Ammo HUD";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? LogSource { get; private set; }

    public override void Load()
    {
        LogSource = Log;
        ClassInjector.RegisterTypeInIl2Cpp<AmmoHudBehaviour>();
        AddComponent<AmmoHudBehaviour>();
        Log.LogInfo($"{PluginName} loaded.");
    }
}

public sealed class AmmoHudBehaviour : MonoBehaviour
{
    private GUIStyle? _mainStyle;
    private GUIStyle? _shadowStyle;
    private string _display = "";
    private float _nextRefresh;

    public AmmoHudBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        if (Time.time < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.time + 0.08f;
        _display = BuildAmmoText();
        TryUpdateNativeAmmoText(_display);
    }

    private void OnGUI()
    {
        if (string.IsNullOrWhiteSpace(_display))
        {
            return;
        }

        EnsureStyles();

        const float width = 190f;
        const float height = 48f;
        float x = Screen.width - width - 34f;
        float y = Screen.height - height - 42f;
        var rect = new Rect(x, y, width, height);

        var oldColor = GUI.color;
        GUI.color = new Color(0.03f, 0.035f, 0.03f, 0.42f);
        GUI.DrawTexture(new Rect(x - 10f, y - 5f, width + 20f, height + 10f), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _display, _shadowStyle);
        GUI.Label(rect, _display, _mainStyle);
    }

    private string BuildAmmoText()
    {
        try
        {
            var controller = PlayerController.currentController;
            var soldier = controller?.ControlledCharacter;
            if (soldier == null || !soldier.IsFPSPlayer() || !soldier.HasHeldItem())
            {
                return "";
            }

            var held = soldier.GetHeldItem_inventory();
            if (held == null)
            {
                return "";
            }

            var heldObject = soldier.GetHeldItem();
            var gun = heldObject?.TryCast<GenericGun>();
            int loaded = gun != null ? Math.Max(0, gun.GetCurrentAmmoCount()) : Math.Max(0, held.chamberedAmmo);
            int reserve = CountReserveAmmo(soldier, held, gun);

            if (held.TryCast<VirtualMagazineGunWeapon>() is { } magWeapon)
            {
                var magazine = magWeapon.installedMagazine;
                if (gun == null && magazine != null)
                {
                    loaded += Math.Max(0, magazine.GetAmmoCount());
                }
            }

            string weaponName = CleanWeaponName(held.GetName());
            return string.IsNullOrWhiteSpace(weaponName)
                ? $"{loaded} / {reserve}"
                : $"{weaponName.ToUpperInvariant()}\n{loaded} / {reserve}";
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogDebug($"Ammo refresh failed: {ex.Message}");
            return "";
        }
    }

    private static string CleanWeaponName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var lines = name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string cleaned = "";
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.IndexOf("magazine installed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            cleaned = string.IsNullOrWhiteSpace(cleaned) ? line : $"{cleaned} {line}";
        }

        return cleaned.Trim();
    }

    private static int CountReserveAmmo(Soldier soldier, VirtualGunWeapon held, GenericGun? gun)
    {
        try
        {
            var inventory = soldier.inventory;
            if (inventory == null)
            {
                return 0;
            }

            string magazineId = "";
            string ammoId = "";
            VirtualMagazineItem? installedMagazine = null;
            bool isMagazineWeapon = false;
            bool hasMagazineSocket = false;
            bool canLoadLooseAmmo = false;

            if (held.TryCast<VirtualMagazineGunWeapon>() is { } magWeapon)
            {
                isMagazineWeapon = true;
                installedMagazine = magWeapon.installedMagazine;
                magazineId = installedMagazine?.item_id ?? "";
            }

            if (gun != null)
            {
                ammoId = gun.compatibleAmmo ?? "";
                hasMagazineSocket = !string.IsNullOrWhiteSpace(gun.magazineSocket);
                canLoadLooseAmmo = gun.allowTopLoadIntoInstalledMagazine || !hasMagazineSocket;
            }

            if (string.IsNullOrWhiteSpace(magazineId) && gun != null && hasMagazineSocket)
            {
                var bestMagazine = inventory.FindBestMagazine(gun.magazineSocket, true);
                magazineId = bestMagazine?.item_id ?? "";
            }

            int reserve = CountMatchingMagazines(inventory, magazineId, installedMagazine);

            if (canLoadLooseAmmo && (!isMagazineWeapon || gun?.allowTopLoadIntoInstalledMagazine == true))
            {
                reserve += CountLooseAmmo(inventory, ammoId);
            }

            return reserve;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountMatchingMagazines(InventoryManager inventory, string magazineId, VirtualMagazineItem? installedMagazine)
    {
        if (string.IsNullOrWhiteSpace(magazineId))
        {
            return 0;
        }

        int reserve = 0;
        var magazines = inventory.GetItemsOfType<VirtualMagazineItem>();
        if (magazines == null)
        {
            return reserve;
        }

        for (int i = 0; i < magazines.Length; i++)
        {
            var magazine = magazines[i];
            if (magazine == null)
            {
                continue;
            }

            bool sameInstalled = installedMagazine != null && magazine.Pointer == installedMagazine.Pointer;
            if (sameInstalled)
            {
                continue;
            }

            if (magazine.item_id == magazineId)
            {
                reserve += Math.Max(0, magazine.GetAmmoCount());
            }
        }

        return reserve;
    }

    private static int CountLooseAmmo(InventoryManager inventory, string ammoId)
    {
        if (string.IsNullOrWhiteSpace(ammoId))
        {
            return 0;
        }

        int reserve = 0;

        try
        {
            var ammo = inventory.GetItemsOfType<VirtualAmmo>();
            if (ammo != null)
            {
                for (int i = 0; i < ammo.Length; i++)
                {
                    if (ammo[i] != null && ammo[i].item_id == ammoId)
                    {
                        reserve += Math.Max(0, ammo[i].GetAmmoCount());
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            var shells = inventory.GetItemsOfType<VirtualShellAmmo>();
            if (shells != null)
            {
                for (int i = 0; i < shells.Length; i++)
                {
                    if (shells[i] != null && shells[i].item_id == ammoId)
                    {
                        reserve += Math.Max(0, shells[i].GetAmmoCount());
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            var droppable = inventory.GetItemsOfType<VirtualDroppableAmmo>();
            if (droppable != null)
            {
                for (int i = 0; i < droppable.Length; i++)
                {
                    if (droppable[i] != null && droppable[i].item_id == ammoId)
                    {
                        reserve += Math.Max(0, droppable[i].GetStackCount());
                    }
                }
            }
        }
        catch
        {
        }

        return reserve;
    }

    private static void TryUpdateNativeAmmoText(string text)
    {
        try
        {
            var gui = PlayerGUI.instance;
            if (gui?.weapon_ammos == null)
            {
                return;
            }

            gui.weapon_ammos.text = text.Replace("\n", "   ");
            gui.weapon_ammos.color = new Color(0.84f, 0.82f, 0.72f, 0.94f);
        }
        catch
        {
            // The IMGUI overlay remains active if the built-in HUD text is not available.
        }
    }

    private void EnsureStyles()
    {
        if (_mainStyle != null && _shadowStyle != null)
        {
            return;
        }

        _mainStyle = new GUIStyle()
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            richText = false
        };
        _mainStyle.normal.textColor = new Color(0.84f, 0.82f, 0.72f, 0.96f);

        _shadowStyle = new GUIStyle()
        {
            alignment = _mainStyle.alignment,
            fontSize = _mainStyle.fontSize,
            fontStyle = _mainStyle.fontStyle,
            richText = _mainStyle.richText
        };
        _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
    }
}
