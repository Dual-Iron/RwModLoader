using Partiality;
using System.Reflection;

namespace Realm.AssemblyLoading
{
    internal static class StaticFixes
    {
        private static bool stubbed;

        private static readonly MethodBase rwg = typeof(RainWorldGame).GetConstructor(new[] { typeof(ProcessManager) });
        private static readonly MethodBase pm = typeof(ProcessManager).GetConstructor(new[] { typeof(RainWorld) });

        static StaticFixes()
        {
            On.RainWorld.Start += RainWorld_Start;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.ProcessManager.ctor += ProcessManager_ctor;
        }

        public static void PreLoad()
        {
            // Compatibility for all mods that use StaticWorld
            typeof(StaticWorld).TypeInitializer.Invoke(null, null);

            // Initialize partiality manager
            PartialityManager.CreateInstance();

            // Add internally-supported mods
            PartialityManager.Instance.modManager.loadedMods.Add(new() { ModID = "Enum Extender" });
        }

        public static void PostLoad()
        {
            // TODO LOW: replace calling stubbed init hooks for ProcessManager and RainWorldGame with a legitimate call by switching menus when loading mods.

            // Stub initialization hooks
            stubbed = true;

            // Call stubbed init hooks
            var rw = Extensions.RainWorld;
            if (rw != null) {
                rw.Start();
                RainWorld.LoadSetupValues(true);

                if (rw.processManager != null) {
                    pm.Invoke(rw.processManager, new[] { rw });

                    if (rw.processManager.currentMainLoop is RainWorldGame g) {
                        rwg.Invoke(g, new[] { rw.processManager });
                    }
                }
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
