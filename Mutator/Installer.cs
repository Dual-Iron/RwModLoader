using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Mutator.InstallerApi;

namespace Mutator
{
    public static class Installer
    {
        public static void UserRun()
        {
            InstallSelf();

            if (IsPartialityInstalled()) {
                Console.WriteLine("Partiality is installed. Uninstalling.");
                UninstallPartiality();
            }

            if (!IsBepInExInstalled()) {
                Console.WriteLine("RwBepInEx is not installed. Installing.");
                InstallBepInEx();
                Console.WriteLine("Success!");
            } else {
                Console.Write("RwBepInEx is installed. Do you want to uninstall? (y/n) ");

                if (Console.ReadKey(true).Key == ConsoleKey.Y) {
                    UninstallBepInEx();
                    Console.WriteLine("Successfully uninstalled.");
                }
            }
        }

        public static void Install()
        {
            InstallSelf();

            if (IsPartialityInstalled()) {
                UninstallPartiality();
            }

            if (!IsBepInExInstalled()) {
                InstallBepInEx();
            }
        }

        private static void InstallSelf()
        {
            string processPath = Environment.ProcessPath ?? throw new("No process path!");
            string copyToDirectory = RwmodsUserFolder.FullName;
            string destFileName = Path.Combine(copyToDirectory, "Mutator.exe");

            if (processPath != destFileName)
                File.Copy(processPath, destFileName, true);
        }

        private static bool IsPartialityInstalled()
        {
            if (Directory.Exists(Path.Combine(RwDir, "RainWorld_Data", "Managed_backup"))) {
                return true;
            }

            string modsDir = Path.Combine(RwDir, "Mods");
            if (!Directory.Exists(modsDir)) {
                return false;
            }

            foreach (var item in Directory.GetFiles(modsDir)) {
                var ext = Path.GetExtension(item);
                if (ext == ".modHash" || ext == ".modMeta") {
                    return true;
                }
            }

            return false;
        }

        private static void UninstallPartiality()
        {
            Directory.Delete(Path.Combine(RwDir, "RainWorld_Data", "Managed"), true);
            Directory.Move(Path.Combine(RwDir, "RainWorld_Data", "Managed_backup"), Path.Combine(RwDir, "RainWorld_Data", "Managed"));

            File.Delete(Path.Combine(RwDir, "consoleLog.txt"));
            File.Delete(Path.Combine(RwDir, "exceptionLog.txt"));

            if (Directory.Exists(Path.Combine(RwDir, "PartialityHashes")))
                Directory.Delete(Path.Combine(RwDir, "PartialityHashes"), true);

            if (Directory.Exists(Path.Combine(RwDir, "ModDependencies")))
                Directory.Delete(Path.Combine(RwDir, "ModDependencies"), true);

            foreach (var item in Directory.GetFiles(Path.Combine(RwDir, "Mods"))) {
                var ext = Path.GetExtension(item);
                if (ext == ".modHash" || ext == ".modMeta") {
                    File.Delete(item);
                }
            }
        }

        public static async Task AwaitKill(string pid)
        {
            // Give the old process some time to die
            using Process p = Process.GetProcessById(int.Parse(pid));

            // That being 1000 ms
            using CancellationTokenSource cts = new(1000);

            try {
                await p.WaitForExitAsync(cts.Token);
            } catch { }

            // If it's not dead, kill it
            if (!p.HasExited) {
                p.Kill(false);
            }
        }

        public static async Task NeedsSelfUpdate()
        {
            await VerifyInternetConnection();

            RepoFiles files = await GetFilesFromGitHubRepository("Dual-Iron", "RwModLoader");
            FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

            bool needs = files.Version > new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart);

            Console.WriteLine(needs ? "y" : "n");
        }

        public static async Task SelfUpdate(IEnumerator<string> args)
        {
            await VerifyInternetConnection();

            RepoFiles files = await GetFilesFromGitHubRepository("Dual-Iron", "RwModLoader");
            FileVersionInfo myVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? throw new("No process path."));

            // Abort if there's nothing to update
            if (files.Version <= new Version(myVersion.ProductMajorPart, myVersion.ProductMinorPart, myVersion.ProductBuildPart)) {
                return;
            }

            // Provide the new process with this process's arguments
            StringBuilder processArgs = new($"--replace {Environment.ProcessId}");

            while (args.MoveNext()) {
                processArgs.Append($" \"{args.Current}\"");
            }

            // Get a safe temp file name
            string tempFileName = Path.Combine(Path.GetTempPath(), "~rwtemp");

            // Download to that file
            using (Stream download = await files.GetOnlineFileStream(0))
            using (Stream tempFile = File.Create(tempFileName))
                await download.CopyToAsync(tempFile);

            // Run the file
            using Process p = Process.Start(new ProcessStartInfo(tempFileName, processArgs.ToString()) {
                UseShellExecute = false
            }) ?? throw new("No process created.");
        }

        public static void UninstallBepInEx()
        {
            if (!IsBepInExInstalled()) {
                return;
            }

            Directory.Delete(Path.Combine(RwDir, "BepInEx"), true);
            File.Delete(Path.Combine(RwDir, "winhttp.dll"));
            File.Delete(Path.Combine(RwDir, "doorstop_config.ini"));
        }

        private static bool IsBepInExInstalled()
        {
            string bepInExCoreDirectory = Path.Combine(RwDir, "BepInEx", "core");
            if (!Directory.Exists(bepInExCoreDirectory)) {
                return false;
            }

            string patcherFilePath = Path.Combine(RwDir, "BepInEx", "patchers", "Realm.dll");
            if (!File.Exists(patcherFilePath)) {
                return false;
            }

            try {
                var bepInEx = AssemblyName.GetAssemblyName(Path.Combine(bepInExCoreDirectory, "BepInEx.dll"));
                return bepInEx.Name == "BepInEx" && bepInEx.Version >= new Version(5, 4, 15);
            } catch (BadImageFormatException) {
                return false;
            } catch (FileNotFoundException) {
                return false;
            }
        }

        private static void InstallBepInEx()
        {
            UninstallBepInEx();

            try {
                using Stream rwbep = typeof(Installer).Assembly.GetManifestResourceStream("RwBep") ?? throw new("No stream!");
                using ZipArchive archive = new(rwbep, ZipArchiveMode.Read, true, UseEncoding);
                archive.ExtractToDirectory(RwDir, true);
            } catch (AggregateException e) {
                throw e.Flatten();
            }
        }
    }
}
