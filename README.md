# Easy Red 2 Plugins

BepInEx IL2CPP plugins for **Easy Red 2**.

## Plugins

| Plugin | Description |
| --- | --- |
| `EasyRed2AmmoHud` | Adds a compact FPS-style infantry weapon ammo display with weapon name, loaded ammo, and matching reserve ammo only. |
| `EasyRed2HealthTweaks` | Adds a BF3-style player HP bar, disables bleeding, blocks the downed state, regenerates living soldiers back to 100 HP, and adds BF4-style look-at-body charged syringe revives on `E` for teammates only plus revive markers. |
| `EasyRed2InfiniteStamina` | Keeps stamina full, prevents out-of-stamina blocking, restores normal mouse look speed while jumping, and allows sprinting while reloading. |
| `EasyRed2ReloadSwitch` | Lets the player switch weapons while reloading by interrupting the reload when a weapon-switch input is pressed. |
| `EasyRed2Spotting` | Adds BF3/BF4-style quick spotting on `Q` using the game's native spotting behavior. |

## Requirements

- Easy Red 2 on Steam
- BepInEx 6 IL2CPP for Windows x64
- A first BepInEx game launch so interop files are generated

Default Steam path used by the projects:

```text
C:\Program Files (x86)\Steam\steamapps\common\Easy Red 2
```

## Install BepInEx

1. Download a BepInEx 6 IL2CPP Windows x64 build from the official builds page:
   <https://builds.bepinex.dev/projects/bepinex_be>
2. Extract it into the Easy Red 2 game folder, next to `Easy Red 2.exe`.
3. Start Easy Red 2 once, then close it.
4. Confirm these folders exist:
   - `BepInEx\plugins`
   - `BepInEx\interop`

## Install Plugins

Copy the DLLs from `dist/` into matching folders under:

```text
Easy Red 2\BepInEx\plugins
```

Recommended layout:

```text
Easy Red 2
+-- BepInEx
    +-- plugins
        +-- EasyRed2AmmoHud
        |   +-- EasyRed2AmmoHud.dll
        +-- EasyRed2HealthTweaks
        |   +-- EasyRed2HealthTweaks.dll
        +-- EasyRed2InfiniteStamina
        |   +-- EasyRed2InfiniteStamina.dll
        +-- EasyRed2ReloadSwitch
        |   +-- EasyRed2ReloadSwitch.dll
        +-- EasyRed2Spotting
            +-- EasyRed2Spotting.dll
```

After launching the game, check:

```text
Easy Red 2\BepInEx\LogOutput.log
```

You should see:

```text
Loading [Easy Red 2 Ammo HUD 0.1.0]
Loading [Easy Red 2 Health Tweaks 0.1.0]
Loading [Easy Red 2 Infinite Stamina 0.1.0]
Loading [Easy Red 2 Reload Switch 0.1.0]
Loading [Easy Red 2 Spotting 0.1.0]
```

## Build From Source

Install the .NET 8 SDK, then build:

```powershell
dotnet build .\plugins\EasyRed2AmmoHud\EasyRed2AmmoHud.csproj -c Release
dotnet build .\plugins\EasyRed2HealthTweaks\EasyRed2HealthTweaks.csproj -c Release
dotnet build .\plugins\EasyRed2InfiniteStamina\EasyRed2InfiniteStamina.csproj -c Release
dotnet build .\plugins\EasyRed2ReloadSwitch\EasyRed2ReloadSwitch.csproj -c Release
dotnet build .\plugins\EasyRed2Spotting\EasyRed2Spotting.csproj -c Release
```

If Easy Red 2 is installed somewhere else:

```powershell
dotnet build .\plugins\EasyRed2AmmoHud\EasyRed2AmmoHud.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
dotnet build .\plugins\EasyRed2HealthTweaks\EasyRed2HealthTweaks.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
dotnet build .\plugins\EasyRed2InfiniteStamina\EasyRed2InfiniteStamina.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
dotnet build .\plugins\EasyRed2ReloadSwitch\EasyRed2ReloadSwitch.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
dotnet build .\plugins\EasyRed2Spotting\EasyRed2Spotting.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
```

Copy the built DLLs from `bin\Release\net6.0\` to `BepInEx\plugins`.

## Notes

- These are client-side BepInEx plugins.
- In `EasyRed2HealthTweaks`, look at a dead teammate and press or hold `E`. A small charge bar appears near the center of the screen; releasing early revives in stepped tiers of 20, 40, 60, or 80 HP, and a full bar revives with 100 HP. Revive markers and revive range follow the moving ragdoll body, so explosions that throw a body also move the revive zone. The syringe has a short cooldown after each revive. AI soldiers also try to move to, charge, and revive same-faction dead soldiers before the marker timer expires. Enemy soldiers are ignored by markers, player revives, and AI revives.
- In `EasyRed2Spotting`, tap or hold `Q` while looking toward an enemy to use Easy Red 2's native spotting behavior. The plugin does not draw `SPOTTED` or `NO TARGET` text.
- Multiplayer servers or anti-cheat rules may not allow gameplay-altering plugins. Use responsibly.
- Built for the current Easy Red 2 IL2CPP/BepInEx setup generated from the local Steam install.
