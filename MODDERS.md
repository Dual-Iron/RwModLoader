# Hot reloading
### Preface
Some mods might not play nice with hot reloading. That's on them. That's also why this feature is opt-in and unstable.

Additionally, the game's memory consumption will increase after every reload. You can alleviate this by setting your mod's static fields to `default` in OnDisable. You don't have to reset [unmanaged](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types) fields.

You don't have to undo any MonoMod hooks when unloading. Realm undoes them automatically.

### How to use
To enable hot reloading:
1. Ensure Rain World is closed.
2. In the file `Rain World/BepInEx/config/Realm.cfg`, set HotReloading under the General section to `true`.

To hot reload:
1. Enter the pause menu in-game.
2. Modify RWMOD files. You can drop the necessary DLL files in `Rain World/BepInEx/plugins` where they'll get wrapped automatically. Alternatively, you can wrap them through a shell using the mutator's `--wrap` command. Run `"%appdata%/.rw/mutator" --help` for more information.
3. Click HOT RELOAD in-game. It's the leftmost button on the pause menu.

### How to pass state between reloads
Some mods have important information that they need to keep track of—even between reloads.

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
2. Just before your mod's disable method is called, GetReloadState() is called.
3. A new copy of your mod is enabled.
4. The result from GetReloadState() is passed into the new mod through Reload(object).

I suggest copy-pasting the examples above into your mod class and editing their method bodies to suit your needs.
