![Number of downloads](https://img.shields.io/github/downloads/Dual-Iron/RwModLoader/total?style=flat&color=blue)
![Latest version](https://img.shields.io/github/v/release/Dual-Iron/RwModLoader?display_name=tag&sort=semver)

# Realm
Installing [Rain World](https://rainworldgame.com/) mods can be a pain, so Realm is my attempt at simplifying the process. It's easy to use for players and developers alike.

To compare Realm with other similar projects:
- [BOI](https://github.com/Rain-World-Modding/BOI) manages mods for Rain World. If you can't use Realm, BOI is a reliable fallback.
- [BepInEx](https://github.com/BepInEx/BepInEx) loads mods for Unity games. Realm uses BepInEx as a framework to bootstrap the vanilla game.
- [Partiality](https://github.com/PartialityModding) loads mods for Rain World and a few other games. It's slowly being replaced by BepInEx, though.

Realm loads and manages your mods in-game, so you can manage mods **without closing the game**. It's a minor miracle this works at all, and there are [a few](#7) mods it breaks. If a mod doesn't work after reloading in-game, restart and avoid reloading while that mod is installed.

There's a **mod browser** to make installing mods easier. You can still install them manually though: Drop the mod's files and dependencies into the plugins folder as one `.zip` file, `.dll` file, or folder. You can find the plugins folder within the mod menu.[<sup>?</sup>](.github/SUPPORT.md)

<details open>
  <summary>Video of reloading</summary>

  https://user-images.githubusercontent.com/31146412/137647603-6034790d-cfcb-40b0-a425-fe113ce7481f.mp4

</details>

## Install Realm
While Realm is installed, keep in mind:
- You don't need to install AutoUpdate, EnumExtender, PublicityStunt, or LogFix.
- You don't need to use BOI. In fact, you shouldn't—just launch the game without it.
- Logs are sent to `BepInEx/LogOutput.log`.

Click [here](https://github.com/Dual-Iron/RwModLoader/releases/latest) to install.

## Uninstall Realm
1. Open the Rain World folder[<sup>?</sup>](https://savelocation.net/steam-game-folder).
2. Run `uninstall_realm.bat` while the game is closed.

## Resources
- Skim the [dev guide](DEVELOPERS.md) to enable reloading from the pause menu.
- Run the [rdb client](https://github.com/Dual-Iron/rdb-client#readme) to submit mods to the browser.
- Visit the [support page](.github/SUPPORT.md) to report bugs, suggest features, or get technical support.

## Credits
- @Dual-Iron
- @Moonburm for [the download icon](Realm/Assets/DOWNLOAD.png)
- @henpemaz for [the homepage icon](Realm/Assets/LINK.png)
- [Rain World](https://rainworldgame.com) and [its community](https://discord.gg/rainworld)
- [BepInEx](https://github.com/BepInEx/BepInEx/tree/v5-lts) and its dependencies
