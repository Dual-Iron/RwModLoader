using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Realm.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;

[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]

namespace Realm;

static class EntryPoint
{
    private static List<string> earlyWrappedAsms = new();
    private static bool initialized;
    private static bool chainloaderHooked;

    public static IEnumerable<string> TargetDLLs => new string[0]; // Don't request anything

    public static void Patch(AssemblyDefinition _) { } // Not used since there is nothing to patch

    public static void Initialize()
    {
        if (initialized) {
            return;
        }

        initialized = true;

        // Have to use EmptyProgressable and can't log the result here.
        ModLoading.PluginWrapper.WrapPlugins(new Progressable(), out earlyWrappedAsms);

        // Can't reference or hook Chainloader before it's been initialized on its own or the game bluescreens
        // So, instead, just track the logger that Chainloader uses.
        new Hook(typeof(Logger).GetMethod("LogMessage", BindingFlags.NonPublic | BindingFlags.Static), typeof(EntryPoint).GetMethod(nameof(Logger_Log)));
    }

    public static void Logger_Log(Action<object> orig, object data)
    {
        orig(data);

        if (!chainloaderHooked && data is "Chainloader ready") {
            // By now, it's safe to hook Chainloader.Start()
            chainloaderHooked = true;
            new ILHook(typeof(Chainloader).GetMethod("Start"), ChainLoader_Start);
        }
    }

    private static void ChainLoader_Start(ILContext il)
    {
        // BepInEx should not load plugins (that's what this mod loader is for), so make it start without loading any plugins.
        ILCursor cursor = new(il);

        // Replace `FindPluginTypes(..)` with `new()`
        cursor.GotoNext(MoveType.After, i => i.MatchCall(typeof(TypeLoader).GetMethod("FindPluginTypes").MakeGenericMethod(typeof(PluginInfo))));
        cursor.Emit(OpCodes.Pop);
        cursor.Emit(OpCodes.Newobj, typeof(Dictionary<string, List<PluginInfo>>).GetConstructor(Type.EmptyTypes));

        // Add `EntryPoint.HookChainLoader()` just before the end of the method
        cursor.Index = cursor.Instrs.Count - 1;
        cursor.EmitDelegate(HookChainloader);
    }

    private static void HookChainloader()
    {
        // Eagerly load EnumExtender assembly
        PastebinMachine.EnumExtender.EnumExtender.Test();

        try {
            Program.Main(earlyWrappedAsms);
        }
        catch (Exception e) {
            Program.Logger.LogFatal(e);
        }
    }
}
