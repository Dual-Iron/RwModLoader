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

## Notes
- Dependencies simply aren't checked. If your logs get spammed by TypeLoadExceptions or similar, it's either because you're missing a dependency for the mod that's throwing or because the patcher is bugged. In either case, [make an issue](https://github.com/Dual-Iron/RwModLoader/issues/new/choose).
- Once you have Realm installed, you can edit `Rain World/BepInEx/config/Realm.cfg` to configure it.

## Dear modders
<details>
  <summary>Interfacing with Realm</summary>
  
  For a GitHub repository to be viable for creating a RWMOD:
  - The repository **must** contain at least one full release.
    - The latest full release **must** match [this Regular Expression](https://regexr.com/66e7q) at least once in its body.
  - The latest full release **must** match [this Regular Expression](https://regexr.com/66jb1) at least once in its tag name.
  - Each explicit dependency **must** be included as a binary.

  Here's an example release.
  ```
  TAG:            v1.0.0
  RELEASE TITLE:  Stable release
  DESCRIPTION:    Plugin for Realm. Blah, blah blah blah, mod description.
  BINARIES:       MyMod.dll
                  ConfigMachine.dll
  PRE-RELEASE?    No
  ```
</details>
<details>
  <summary>Enabling in-game mod reloading</summary>

  BEFORE ENABLING, PLEASE READ.

  In practice, checking if your mod is unloaded is simple as having `public static readonly bool LOADED = true;` in some class; if this value is ever false, then your mod is unloaded. If your mod is unloaded, then all static fields will be equal to `default`.

  This is problematic because every time you read from a static field, you must assign it to a local, then check if that local equals `default` unexpectedly. So, while possible, it's incredibly tedious to use static fields. The better alternative is to store your mod's state in an object and pass that around through parameters/fields. The object could be a dedicated reference type or simply your mod instance.

<details>
  <summary>Example in practice</summary>

  ```cs
  // Here's an example of safe practice using static fields.
  [BepInPlugin(...)]
  public sealed class MyPlugin : BaseUnityPlugin {
      public static MyPlugin Instance { get; private set; }    // This will be null after reloading!
      public static string MyString { get; } = "Hello World!"; // This will be null after reloading!

      void OnEnable() {
          Instance = this;
          // You would normally apply hooks here.
      }

      void Update() => Run();

      static void Run() {
          // This code is unsafe.
          Console.WriteLine(Instance.MyString.Length);

          // This code is unsafe, too.
          if (Instance.MyString != null) {
              // Because unloading assemblies is multithreaded, Instance.MyString could be set to null right after checking for it!
              Console.WriteLine(Instance.MyString.Length);
          }

          // This code is safe.
          var str = Instance.MyString;
          if (str != null) {
              Console.WriteLine(str.Length);
          }
      }
  }
  ```

  ```cs
  // Here's a much simpler example using instance fields.
  [BepInPlugin(...)]
  public sealed class MyPlugin : BaseUnityPlugin {
      public string MyString { get; } = "Hello World!"; // This is an instance field, not a static field, so Realm won't touch it. It's safe to use.

      void OnEnable() {
          // You would normally apply hooks here.
      }

      void Update() => Run(this);

      static void Run(MyPlugin plugin) {
          // This code is safe.
          Console.WriteLine(plugin.MyString.Length);
      }
  }
  ```
</details>

  Okay, now that you know how to prevent catastrophic failure when reloading mods, let's do it.

  To enable hot reloading:
  1. Ensure Rain World is closed.
  2. In the file `Rain World/BepInEx/config/Realm.cfg`, set HotReloading under the General section to `true`.

  To do it:
  1. Enter the pause menu in-game.
  2. Modify/overwrite any relevant RWMOD files in the `%appdata%/.rw/mods` folder.
  3. Click HOT RELOAD in-game.
</details>

## Credits
- Dual, for being such a nerd.
- Thrithralas, for the MODS and RAINDB headers.
- Pastebee, for [this monstrosity](https://github.com/Dual-Iron/RwModLoader/blob/5e13a516436f7c7e75403f383e6ec34570a07eec/Mutator/Patching/AccessViolationPrevention.cs#L8).
- [BepInEx](https://github.com/BepInEx/BepInEx/tree/v5-lts) and its dependencies, for making this possible.
- [Rain World](https://rainworldgame.com/) and [its community](https://discord.gg/rainworld), for making this worthwhile.
