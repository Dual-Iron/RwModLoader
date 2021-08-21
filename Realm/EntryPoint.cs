using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;

[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]

namespace Realm
{
    public static class EntryPoint
    {
        private static Hook? hook;
        private static bool chainloaderHooked = false;

        public static IEnumerable<string> TargetDLLs => new string[0]; // Don't request anything

        public static void Patch(AssemblyDefinition _) { } // Not used since there is nothing to patch

        public static void Finish()
        {
            if (hook != null) {
                return;
            }

            // Can't reference or hook Chainloader before it's been initialized on its own or the game bluescreens
            // So, instead, just track the logger that Chainloader uses.
            hook = new Hook(typeof(Logger).GetMethod("LogMessage", BindingFlags.NonPublic | BindingFlags.Static), typeof(EntryPoint).GetMethod(nameof(Logger_Log)));
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
            cursor.EmitDelegate((Action)HookChainloader);
        }

        private static void HookChainloader()
        {
            static Assembly? Resolve(object sender, ResolveEventArgs args)
            {
                try {
                    string shortname = args.Name.Substring(0, args.Name.IndexOf(','));
                    string path = Path.Combine(Extensions.RwDepFolder, Path.ChangeExtension(shortname, ".dll"));
                    return File.Exists(path) ? Assembly.LoadFile(path) : null;
                } catch (Exception e) {
                    Program.Logger.LogFatal("Assembly resolution exception: " + e);
                    return null;
                }
            }

            // When a method is called, the runtime checks to see if any of its opcodes refer to unresolved types and tries to resolve them,
            // so you have to provide assembly resolvers before even touching Main, in case you accidentally refer to an unresolved assembly.
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            try {
                Program.Main();
            } catch (Exception e) {
                Program.Logger.LogFatal(e);
            }
        }
    }
}
