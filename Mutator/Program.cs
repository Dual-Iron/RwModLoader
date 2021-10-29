global using static Mutator.InstallerApi;
using Mutator.IO;
using Mutator.Patching;
using System.Diagnostics;

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

        if (e is HttpRequestException hre) {
            Console.Error.WriteLine($"HTTP request error {(int?)hre.StatusCode}: {hre.Message}");
            return ExitCodes.ConnectionFailed;
        }

        if (e is IOException) {
            Console.Error.WriteLine(e.Message);
            return ExitCodes.IOError;
        }

        if (e is BadExecutionException bee) {
            Console.Error.WriteLine(e.Message);
            return bee.ExitCode;
        }

        Console.WriteLine("The mutator encountered an internal error. " + e.Message);
        Console.Error.WriteLine(e);
        return ExitCodes.InternalError;
    }

    private static void Run(string[] args)
    {
        if (args.Length == 0 && Path.GetFileName(Environment.ProcessPath) != "Mutator.exe") {
            Console.WriteLine("Working...");

            // Eagerly load RwDir to ensure it's valid before installing
            _ = RwDir;

            Installer.Install();

            Console.Write("Installed the newest release of Realm. Press ENTER to start Rain World or ESC to exit. ");

            if (Console.ReadKey(true).Key == ConsoleKey.Enter) {
                using var p = Process.Start(new ProcessStartInfo {
                    FileName = "steam://run/312520",
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            return;
        }

        if (args.Length == 1 && (args[0] == "?" || args[0] == "--help" || args[0] == "help")) {
            ListHelp();
            return;
        }

        using IEnumerator<string> enumerator = ((IReadOnlyList<string>)args).GetEnumerator();

        while (enumerator.MoveNext()) {
            Task? task = null;

            string arg0 = enumerator.Current;

            if (arg0 == "--install")
                task = Task.Run(Installer.Install);
            else if (arg0 == "--uninstall")
                task = Task.Run(Installer.UninstallBepInEx);
            else if (arg0 == "--needs-self-update")
                task = Installer.NeedsSelfUpdate();
            else if (arg0 == "--self-update")
                task = Installer.SelfUpdate(enumerator);
            else if (arg0 == "--extract-all")
                task = Extracting.ExtractAll();
            else if (arg0 == "--runrw")
                task = Task.Run(() => Installer.RunRw());
            else if (enumerator.MoveNext()) {
                string arg1 = enumerator.Current;

                if (arg0 == "--kill")
                    task = Task.Run(() => Installer.Kill(arg1));
                else if (arg0 == "--patch")
                    task = Task.Run(() => AssemblyPatcher.Patch(arg1));
                else if (arg0 == "--extract")
                    task = Task.Run(() => Extracting.Extract(arg1));
                else if (enumerator.MoveNext()) {
                    string arg2 = enumerator.Current;

                    if (arg0 == "--wrap")
                        task = Wrapper.Wrap(arg1, arg2);
                }
            }

            if (task == null) {
                throw Err(ExitCodes.InvalidArgs, $"Unknown command '{arg0}' with those parameters. Use '--help' for a list of commands.");
            }

            task.Wait();
        }
    }

    private static void ListHelp()
    {
        Console.WriteLine($@"
RwmlMutator v{typeof(Program).Assembly.GetName().Version}

--help                You're here!
--install             Installs Realm.
--uninstall           Uninstalls Realm.
--needs-self-update   Prints 'y' if Realm needs an update or 'n' if not.
--self-update         Updates Realm.
--extract-all         Extracts the contents of all RWMOD files in the mods folder.
--patch [path]        Patches the .NET assembly.
--extract [rwmod]     Extracts the contents of the RWMOD.
--wrap [rwmod] [path] Wraps the DLL or directory specified at [path] into a RWMOD.
"
);
    }
}
