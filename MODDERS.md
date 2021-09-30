# Interfacing with Realm via GitHub
For a mod to interface with Realm, its homepage on raindb.net must be a GitHub repository, and:
- The repository **must** contain at least one full release.
- The latest full release **must** match [this Regular Expression](https://regexr.com/66e7q) at least once in its body.
- The latest full release **must** match [this Regular Expression](https://regexr.com/66jb1) at least once in its tag name.
- Each explicit dependency **must** be included as a binary.

Mods that interface with Realm are automatically updated and enjoy one-click downloads in the browser.

### Example release
```
TAG:            v1.0.0
RELEASE TITLE:  First stable release!!
DESCRIPTION:    Plugin for Realm. Blah, mod description, blah blah changelog blah.
BINARIES:       MyMod.dll
                ConfigMachine.dll
PRE-RELEASE:    No
```

# Using hot reloading

### Important note before doing the thing
Some mods might not play nice with hot reloading. That's on them. That's also why this feature is opt-in and unstable.

Additionally, because assemblies are [never truly unloaded from the application domain](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-5.0#remarks), the game *will* eventually run out of memory and crash. To reduce the memory load, avoid using static fields. Instead, opt for passing around an object that represents your mod's state. This lets the garbage collector properly collect your mod after it has been unloaded.

### How to do the thing
To enable hot reloading:
1. Ensure Rain World is closed.
2. In the file `Rain World/BepInEx/config/Realm.cfg`, set HotReloading under the General section to `true`.

To hot reload:
1. Enter the pause menu in-game.
2. Modify/overwrite RWMOD files as you please in the `%appdata%/.rw/mods` folder. This can be done by wrapping your mod (see below).
3. Click HOT RELOAD in-game. It's to the left of the EXIT button.

### Wrapping mods as RWMOD files
Add this to your csproj file just above `</Project>` (works with SDK-style projects). This lets you build your project then immediately hot reload it in-game. Make sure to replace "MyMod" with your mod's name.
```csproj
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="&quot;%appdata%/.rw/mutator&quot; --wrap MyMod &quot;$(TargetDir)$(TargetName).dll&quot;" />
  </Target>
```

You can learn more about the --wrap command by running `"%appdata%/.rw/mutator" --help`.
