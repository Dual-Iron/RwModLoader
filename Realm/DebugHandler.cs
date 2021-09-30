using System.Diagnostics;
using UnityEngine;

namespace Realm;

public static class DebugHandler
{
    public static void Hook()
    {
        On.RainWorld.Update += RainWorld_Update;
    }

    private static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        if (Input.GetKeyDown(KeyCode.F3)) {
            PrintDebug();
        }

        orig(self);
    }

    private static void PrintDebug()
    {
        Console.WriteLine($@"
-- DEBUG INFO -- {DateTime.Now:T} --
Managed memory: {GC.GetTotalMemory(false) / 1000000L,4} MB / 2000 MB
");
    }
}
