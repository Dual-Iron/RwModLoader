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

## I need help!
Look in the [support](.github/SUPPORT.md) section.

## Dear modders,
Go [here](MODDERS.md).

## Future plans
- Autoupdating for GitHub mods
- Mod templates

## Notes
- Dependencies simply aren't checked. If your logs get spammed by TypeLoadExceptions or similar, it's either because you're missing a dependency for the mod that's throwing or because the patcher is bugged. In either case, [make an issue](https://github.com/Dual-Iron/RwModLoader/issues/new/choose).
- Once you have Realm installed, you can edit `Rain World/BepInEx/config/Realm.cfg` to configure it.

## Credits
- Dual, for being such a nerd.
- Thrithralas, for the MODS and RAINDB headers.
- Pastebee, for [this monstrosity](https://github.com/Dual-Iron/RwModLoader/blob/5e13a516436f7c7e75403f383e6ec34570a07eec/Mutator/Patching/AccessViolationPrevention.cs#L8).
- [BepInEx](https://github.com/BepInEx/BepInEx/tree/v5-lts) and its dependencies, for making this possible.
- [Rain World](https://rainworldgame.com/) and [its community](https://discord.gg/rainworld), for making this worthwhile.
