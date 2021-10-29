# Using hot reloading

### Important note before doing the thing
Some mods might not play nice with hot reloading. That's on them. That's also why this feature is opt-in and unstable.

Additionally, because assemblies are [never truly unloaded from the application domain](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-5.0#remarks), the game *will* eventually run out of memory and crash. To reduce the memory load, reset static fields to `default` in OnDisable. This lets your mod be properly garbage collected after Realm unloads it.

### How to do the thing
To enable hot reloading:
1. Ensure Rain World is closed.
2. In the file `Rain World/BepInEx/config/Realm.cfg`, set HotReloading under the General section to `true`.

To hot reload:
1. Enter the pause menu in-game.
2. Modify RWMOD files. You can drop the necessary DLL files in `Rain World/BepInEx/plugins` where they'll get wrapped automatically. Alternatively, you can wrap them through a shell using the mutator's `--wrap` command. Run `"%appdata%/.rw/mutator" --help` for more information.
3. Click HOT RELOAD in-game. It's the leftmost button on the pause menu.

### How to pass state between reloads
Some mods have improtant information that they need to keep track of—even between reloads. This API is a reliable way to do that by passing an object between old and new assemblies.

⚠ **This is important!** If you return an object whose type is from a mod assembly, then you won't be able to access any of that object's members easily. Opt for passing simple primitives and collections instead.

```cs
// Must not have a void return type or any parameters
// Must be named "GetReloadState"
// Must be public and instance
// Example:
public object GetReloadState() => new object();

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

# Interfacing with Realm
<details>
  <summary>This isn't implemented yet, but you might want to read it anyway</summary>
  
# Via GitHub
For a mod to interface with Realm, its homepage on raindb.net must be a GitHub repository, and:
- The repository must contain at least one full release.
- The latest full release must match [this Regular Expression](https://regexr.com/66e7q) at least once in its body.
- Every dependency must be included as a binary.

### Example release
```
TAG:            v1.0.0
RELEASE TITLE:  First stable release!!
DESCRIPTION:    Plugin for Realm. Blah, mod description, blah blah changelog blah.
BINARIES:       MyMod.dll
                ConfigMachine.dll
PRE-RELEASE:    No
```
</details
