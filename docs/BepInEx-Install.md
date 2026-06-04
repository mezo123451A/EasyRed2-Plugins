# BepInEx Setup For Easy Red 2

Easy Red 2 is a Unity IL2CPP game, so use **BepInEx 6 IL2CPP**, not the Mono build.

## Steps

1. Download BepInEx 6 IL2CPP Windows x64 from:
   <https://builds.bepinex.dev/projects/bepinex_be>
2. Extract the archive into the Easy Red 2 folder.
3. The game folder should contain:
   - `BepInEx`
   - `winhttp.dll`
   - `doorstop_config.ini`
4. Start Easy Red 2 once.
5. Close the game after it reaches the menu.
6. Confirm `BepInEx\interop` exists. The plugin projects reference these generated interop assemblies.

Default game folder:

```text
C:\Program Files (x86)\Steam\steamapps\common\Easy Red 2
```
