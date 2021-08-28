﻿using MonoMod.RuntimeDetour;
using Partiality;
using Partiality.Modloader;
using System;
using System.Reflection;

namespace Realm.AssemblyLoading
{
    internal static class StaticFixes
    {
        private static bool stubbed;

        private static readonly MethodBase rwg = typeof(RainWorldGame).GetConstructor(new[] { typeof(ProcessManager) });
        private static readonly MethodBase pm = typeof(ProcessManager).GetConstructor(new[] { typeof(RainWorld) });
        private static readonly PartialityMod ee = new() { ModID = "Enum Extender" };

        public static void Hook()
        {
            On.RainWorld.Start += RainWorld_Start;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.ProcessManager.ctor += ProcessManager_ctor;

            new Hook(typeof(Assembly).GetProperty("Location").GetGetMethod(), (Func<Func<Assembly, string>, Assembly, string>)HookGetLocation).Apply();
        }

        private static string HookGetLocation(Func<Assembly, string> orig, Assembly self)
        {
            var ret = orig(self);

            var asmPool = ProgramState.Current.Mods.AssemblyPool;

            if (asmPool != null && string.IsNullOrEmpty(ret)) {
                string name = self.GetName().Name;
                int index = name.IndexOf(AssemblyPool.IterationSeparator);
                if (index != -1) {
                    name = name.Substring(0, index);

                    foreach (var asmName in asmPool.Names) {
                        if (asmName == name) {
                            return asmPool[asmName].Path;
                        }
                    }
                }
            }

            return ret;
        }

        public static void PreLoad()
        {
            try {
                // Compatibility for all mods that use StaticWorld
                typeof(StaticWorld).TypeInitializer.Invoke(null, null);
            } catch (Exception e) {
                Program.Logger.LogError(e);
            }

            // Initialize partiality manager
            PartialityManager.CreateInstance();

            // Add internally-supported mods
            if (!PartialityManager.Instance.modManager.loadedMods.Contains(ee)) {
                PartialityManager.Instance.modManager.loadedMods.Add(ee);
            }
        }

        public static void PostLoad()
        {
            // TODO LOW: replace calling stubbed init hooks for RainWorldGame with a legitimate call by switching menus when loading mods.

            // Stub initialization hooks
            stubbed = true;

            try {
                // Call stubbed init hooks
                var rw = Extensions.RainWorld;
                if (rw is not null) {
                    rw.Start();
                    RainWorld.LoadSetupValues(true);

                    if (rw.processManager != null) {
                        pm.Invoke(rw.processManager, new[] { rw });

                        if (rw.processManager.currentMainLoop is RainWorldGame g) {
                            rwg.Invoke(g, new[] { rw.processManager });
                        }
                    }
                }
            } catch (Exception e) {
                Program.Logger.LogError(e);
            }

            // Un-stub the init hooks
            stubbed = false;
        }

        private static void ProcessManager_ctor(On.ProcessManager.orig_ctor orig, ProcessManager self, RainWorld rainWorld)
        {
            if (!stubbed)
                orig(self, rainWorld);
        }

        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            if (!stubbed)
                orig(self, manager);
        }

        private static void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
        {
            if (!stubbed)
                orig(self);
            self.setup = RainWorld.LoadSetupValues(true);
        }
    }
}
