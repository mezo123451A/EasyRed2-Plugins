# Easy Red 2 Plugins

BepInEx IL2CPP plugins for **Easy Red 2**.

## Plugins

| Plugin | Description |
| --- | --- |
| `EasyRed2AmmoHud` | Adds a compact FPS-style weapon ammo display with weapon name, loaded ammo, reserve ammo, and vehicle weapon ammo support. |
| `EasyRed2InfiniteStamina` | Keeps stamina full, prevents out-of-stamina blocking, restores normal mouse look speed while jumping, and allows sprinting while reloading with `Shift + W`. |

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
└── BepInEx
    └── plugins
        ├── EasyRed2AmmoHud
        │   └── EasyRed2AmmoHud.dll
        └── EasyRed2InfiniteStamina
            └── EasyRed2InfiniteStamina.dll
```

After launching the game, check:

```text
Easy Red 2\BepInEx\LogOutput.log
```

You should see:

```text
Loading [Easy Red 2 Ammo HUD 0.1.0]
Loading [Easy Red 2 Infinite Stamina 0.1.0]
```

## Build From Source

Install the .NET 8 SDK, then build:

```powershell
dotnet build .\plugins\EasyRed2AmmoHud\EasyRed2AmmoHud.csproj -c Release
dotnet build .\plugins\EasyRed2InfiniteStamina\EasyRed2InfiniteStamina.csproj -c Release
```

If Easy Red 2 is installed somewhere else:

```powershell
dotnet build .\plugins\EasyRed2AmmoHud\EasyRed2AmmoHud.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
dotnet build .\plugins\EasyRed2InfiniteStamina\EasyRed2InfiniteStamina.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
```

Copy the built DLLs from `bin\Release\net6.0\` to `BepInEx\plugins`.

## Notes

- These are client-side BepInEx plugins.
- Multiplayer servers or anti-cheat rules may not allow gameplay-altering plugins. Use responsibly.
- Built for the current Easy Red 2 IL2CPP/BepInEx setup generated from the local Steam install.
