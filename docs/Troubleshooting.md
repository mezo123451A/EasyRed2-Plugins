# Troubleshooting

## Plugins Do Not Load

Open:

```text
Easy Red 2\BepInEx\LogOutput.log
```

Look for lines containing the plugin names. If they are missing, confirm the DLLs are inside `BepInEx\plugins`.

## Build Fails With Missing References

Launch Easy Red 2 once after installing BepInEx. BepInEx generates `BepInEx\interop`, which the projects reference.

If your game is not installed in the default Steam folder, pass `EasyRed2GameDir`:

```powershell
dotnet build .\plugins\EasyRed2AmmoHud\EasyRed2AmmoHud.csproj -c Release -p:EasyRed2GameDir="D:\SteamLibrary\steamapps\common\Easy Red 2"
```

## Stamina Plugin Does Not Change Movement

That is intentional. `EasyRed2InfiniteStamina` only changes stamina, airborne view speed, and sprint-while-reloading. It does not modify A/D sideways movement.
