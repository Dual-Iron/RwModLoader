using Mono.Cecil;
using Mutator.Packaging;
using Mutator.Patching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mutator
{
    public static class Program
    {
        private static int Main(string[] args)
        {
            try {
                Run(args);
                return 0;
            } catch (AssemblyResolutionException e) {
                Console.Error.WriteLine($"E:{e.AssemblyReference}");
                Console.Error.WriteLine($"Could not resolve assembly {e.AssemblyReference.Name} v{e.AssemblyReference.Version}. Try adding it to your Rain World plugins folder.");
                return 1;
            } catch (AggregateException e) {
                foreach (var innerE in e.Flatten().InnerExceptions) {
                    Console.Error.WriteLine(innerE);
                }
                return 2;
            } catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                return -1;
            } finally {
                InstallerApi.Dispose();
            }
        }

        private static void Run(string[] args)
        {
            if (args.Length == 0) {
                Installer.UserRun().Wait();
                return;
            }

            bool parallel = false;

            List<Task> tasks = new();

            IEnumerator<string> enumerator = ((IReadOnlyList<string>)args).GetEnumerator();

            while (enumerator.MoveNext()) {
                Task? task = null;

                string arg0 = enumerator.Current;

                if (arg0 == "--help")
                    ListHelp();
                else if (arg0 == "--parallel") {
                    if (parallel) {
                        Task.WaitAll(tasks.ToArray());
                        tasks.Clear();
                    }
                    parallel = !parallel;
                } else if (arg0 == "--raindb")
                    task = RaindbGetter.Run();
                else if (arg0 == "--install")
                    task = Installer.Install();
                else if (arg0 == "--uninstall")
                    task = Task.Run(Installer.Uninstall);
                else if (arg0 == "--selfupdate")
                    task = Installer.SelfUpdate();
                else if (enumerator.MoveNext()) {
                    string arg1 = enumerator.Current;

                    if (arg0 == "--patch")
                        task = AssemblyPatcher.Patch(arg1, false);
                    else if (arg0 == "--patchup")
                        task = AssemblyPatcher.Patch(arg1, true);
                    else if (arg0 == "--download")
                        task = Packager.Download(arg1);
                    else if (arg0 == "--include")
                        task = Task.Run(() => Packager.Include(arg1));
                    else if (arg0 == "--wrap")
                        task = Task.Run(() => Packager.Wrap(arg1));
                    else if (arg0 == "--unwrap")
                        task = Packager.Unwrap(arg1);
                    else if (arg0 == "--extract")
                        task = Task.Run(() => Packager.Extract(arg1));
                    else if (arg0 == "--restore")
                        task = Task.Run(() => Packager.Restore(arg1));
                    else if (enumerator.MoveNext()) {
                        string arg2 = enumerator.Current;

                        if (arg0 == "--update")
                            task = Packager.Update(arg1, arg2);
                    }
                }

                if (parallel) {
                    if (task != null)
                        tasks.Add(task);
                } else {
                    task?.Wait();
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        private static void ListHelp()
        {
            Console.WriteLine($@"
RwModMutator v{typeof(Program).Assembly.GetName().Version} - Documentation: https://tinyurl.com/rwmmd

help                        You're here!
parallel                    Toggles between asynchronous and synchronous task execution.
raindb                      Lists readily-downloadable mods from RainDB as a list of binary strings.
install                     Installs Realm.
uninstall                   Uninstalls Realm.
selfupdate                  Updates Realm. (Not implemented)
patch [path]                Patches the .NET assembly.
patchup [path]              Patches the .NET assembly, then updates its RWMOD.
download [path]             Downloads the RWMOD's contents.
include [path]              Includes the RWMOD in the user's mods folder.
wrap [path]                 Wraps the .NET assembly or ZIP into a RWMOD.
unwrap [path]               Unwraps the RWMOD.
extract [path]              Extracts the contents of the RWMOD to a folder in the same directory.
restore [path]              Un-unwraps the RWMOD.
update [rwmod name] [path]  Updates or creates the file's entry in the RWMOD with the specified name.
"
);
        }
    }
}