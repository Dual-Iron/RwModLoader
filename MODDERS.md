# Hot reloading
### Preface
Some mods might not play nice with hot reloading. That's on them. That's also why this feature is opt-in and unstable.

The game will consume more memory each reload. You can alleviate this by setting your mod's static fields to `default` in OnDisable. Note that you don't have to reset [unmanaged](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types) fields because that wouldn't free any memory.

You don't have to undo any MonoMod hooks when unloading. Realm undoes them automatically.

<details>
  <summary>Example</summary>
  
  ```cs
  static Player player;
  static int num;
  
  void OnEnable() {
    On.Player.ctor += (orig, plr, a, w) => {
      orig(plr, a, w);
      player = plr;
      num++;
    };
  }

  void OnDisable() {
    // You needn't reset `num` because that frees no memory. You can if you want to, though.
    // num = default;
  
    // You should reset `player` because that frees a lot of memory.
    player = default;
  }
  ```
  
</details>

### How to use
To enable hot reloading:
1. Ensure Rain World is closed.
2. In the file `Rain World/BepInEx/config/Realm.cfg`, set HotReloading under the General section to `true`.

To hot reload:
1. Enter the pause menu in-game.
2. Put modified DLL files in `Rain World/BepInEx/plugins`.
3. Click HOT RELOAD in-game.

### How to pass state between reloads
Some mods pass information between reloads (e.g. [SlugBase](https://github.com/SlimeCubed/SlugBase/blob/f84e8b499f38a296216032faefe93165b0b2dfd7/SlugBase/SlugBaseMod.cs#L95)). If you don't need to do that, you can stop reading here.

<details>
  <summary>Reveal</summary>
  
  ```cs
  // Must not have a void return type or any parameters
  // Must be named "GetReloadState"
  // Must be public and instance
  // Example:
  public object GetReloadState() => new object();
  // ⚠ Only return objects from the System assembly, like `int`, `List<>`, `Dictionary<,>`, and so on.
  
  // Must have exactly one parameter and that parameter must be a System.Object
  // Must be named "Reload"
  // Must be public and instance
  // Example:
  public void Reload(object state) {}
  
  // Both of these must be members of exactly one mod type per assembly
  ```
  
  As long as this contract is fulfilled, you can expect the following behavior:
  1. You reload your mods.
  2. GetReloadState() is called.
  3. Your mod's Disable method is called.
  4. A new copy of your mod is enabled.
  5. The result from GetReloadState() is passed into the new mod through Reload(object).
  
  I suggest copy-pasting the examples above into your mod class and editing their method bodies to suit your needs.
  
</details>

# Interop with rdb (Realm db)
For now, create a webhook using the video below. Then, create or edit your latest release to push it to rdb.

https://user-images.githubusercontent.com/31146412/148865236-a5947045-c4a9-472c-8e53-7c8bd16bf336.mp4
