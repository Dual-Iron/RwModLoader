## Where get?
Go [here](https://github.com/Dual-Iron/RwModLoader/releases/latest).

## Why get?
- Enable, disable, and delete mods without closing the game.
- Use newer versions of BepInEx and MonoMod that are less prone to spontaneous combustion.
- Hot reload assemblies from the in-game pause menu. (This feature is enabled through Realm's BepInEx config.)
- Download fewer dependencies (EnumExtender and PublicityStunt).

## What get?
A modloader for Rain World: RwModLoader, or RWML. Or Realm, because that sounds cool.

https://user-images.githubusercontent.com/31146412/131228787-764c1723-7dca-46e6-894d-bd4472321428.mp4

## Future plans
- Download mods from https://raindb.net
- Autoupdating for GitHub mods
- Integrate a form of ConfigMachine (still iffy about this one)
- Mod templates

## Known issues
- Dependencies simply aren't checked. If your logs get spammed by TypeLoadExceptions or similar, it's either because you're missing a dependency for the mod that's throwing or because the patcher is bugged. In either case, [make an issue](https://github.com/Dual-Iron/RwModLoader/issues/new/choose).
- The mutator can easily corrupt RWMOD files if it throws an exception while updating them. In the case that logs are spammed with `Process exited with code 2: Number of entries expected in End Of Central Directory does not correspond to number of entries in Central Directory` or something similar, close Rain World and run `rd /s /q "%appdata%\.rw\mods"` in CMD.
