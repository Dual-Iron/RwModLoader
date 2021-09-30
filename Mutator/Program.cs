global using static Mutator.InstallerApi;
using Mutator.IO;
using Mutator.ModListing;
using Mutator.Patching;

namespace Mutator;

public static class Program
{
    private static int Main(string[] args)
    {
        try {
            Run(args);
            return 0;
        } catch (Exception e) {
            return (int)HandleError(e);
        } finally {
            Dispose();
        }
    }

    private static ExitCodes HandleError(Exception e)
    {
        if (e is AggregateException ae) {
            ExitCodes last = ExitCodes.InternalError;
            foreach (var innerE in ae.Flatten().InnerExceptions) {
                last = HandleError(innerE);
            }
            return last;
        }

        if (e is IOException) {
            Console.WriteLine(e.Message);
            return ExitCodes.IOError;
        }

        if (e is BadExecutionException bee) {
            Console.WriteLine(e.Message);
            return bee.ExitCode;
        }

        Console.WriteLine("The mutator encountered an internal error. " + e.Message);
        Console.Error.WriteLine(e);
        return ExitCodes.InternalError;
    }

    private static void Run(string[] args)
    {
        if (args.Length == 1 && (args[0] == "?" || args[0] == "--help" || args[0] == "help")) {
            ListHelp();
            return;
        }

        using IEnumerator<string> enumerator = ((IReadOnlyList<string>)args).GetEnumerator();

        while (enumerator.MoveNext()) {
            Task? task = null;

            string arg0 = enumerator.Current;

            if (arg0 == "--raindb")
                task = RaindbMod.PrintAll();
            else if (arg0 == "--install")
                task = Task.Run(Installer.Install);
            else if (arg0 == "--uninstall")
                task = Task.Run(Installer.UninstallBepInEx);
            else if (arg0 == "--needs-self-update")
                task = Installer.NeedsSelfUpdate();
            else if (arg0 == "--self-update")
                task = Installer.SelfUpdate(enumerator);
            else if (enumerator.MoveNext()) {
                string arg1 = enumerator.Current;

                if (arg0 == "--kill")
                    task = Task.Run(() => Installer.Kill(arg1));
                else if (arg0 == "--run")
                    task = Task.Run(() => Installer.Run(arg1));
                else if (arg0 == "--patch")
                    task = Task.Run(() => AssemblyPatcher.Patch(arg1));
                else if (arg0 == "--download")
                    task = Downloading.Download(arg1);
                else if (arg0 == "--extract")
                    task = Task.Run(() => Extracting.Extract(arg1));
                else if (enumerator.MoveNext()) {
                    string arg2 = enumerator.Current;

                    if (arg0 == "--wrap")
                        task = Wrapper.Wrap(arg1, arg2, true);
                }
            }

            if (task == null) {
                Console.Error.WriteLine($"Unknown command '{arg0}'. Use '--help' for a list of commands.");
                return;
            }

            task.Wait();
        }
    }

    private static void ListHelp()
    {
        Console.WriteLine($@"
RwModMutator v{typeof(Program).Assembly.GetName().Version} - Documentation: https://tinyurl.com/rwmmd

--help                You're here!
--raindb              Lists mods from RainDB.
--install             Installs Realm.
--uninstall           Uninstalls Realm.
--needs-self-update   Prints 'y' if Realm needs an update or 'n' if not.
--self-update         Updates Realm.
--kill [pid]          Kills the specified process.
--patch [path]        Patches the .NET assembly.
--download [repo]     Downloads the most recent version of a RWMOD from a GitHub [repo] if need be.
--extract [rwmod]     Extracts the contents of the RWMOD to a folder in the same directory.
--wrap [rwmod] [path] Wraps the DLL or directory specified at [path] into a RWMOD.
"
);
    }
}
