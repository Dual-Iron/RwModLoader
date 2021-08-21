using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using static Mutator.InstallerApi;

namespace Mutator
{
    public static class Installer
    {
        private const string InstallerFileName = "mutator.exe";

        public static async Task UserRun()
        {
            InstallSelf();

            if (IsPartialityInstalled()) {
                Console.WriteLine("Partiality is installed. Uninstalling.");
                UninstallPartiality();
            }

            if (!IsBepInExInstalled()) {
                Console.WriteLine("RwBepInEx is not installed. Installing.");
                await InstallBepInEx();
            } else {
                Console.Write("RwBepInEx is installed. Do you want to uninstall? (y/n) ");

                if (Console.ReadKey(true).Key == ConsoleKey.Y) {
                    Uninstall();
                    Console.WriteLine("Successfully uninstalled.");
                }
            }
        }

        public static async Task Install()
        {
            InstallSelf();

            if (IsPartialityInstalled()) {
                Console.WriteLine("Partiality is installed. Uninstalling.");
                UninstallPartiality();
            }

            if (!IsBepInExInstalled()) {
                Console.WriteLine("RwBepInEx is not installed. Installing.");
                await InstallBepInEx();
            }
        }

        private static void InstallSelf()
        {
            string processPath = Environment.ProcessPath ?? throw new("No process path!");
            string copyToDirectory = GetRwmodsUserFolder().FullName;
            string destFileName = Path.Combine(copyToDirectory, Path.GetFileName(processPath));

            if (!File.Exists(destFileName)) {
                File.Copy(processPath, destFileName);
            }
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

            Console.WriteLine("Successfully uninstalled Partiality.");
        }

        private const string Organization = "Dual-Iron";
        private const string BepRepo = "RwBepInEx";

        public static async Task SelfUpdate()
        {
            throw new NotImplementedException(); // TODO LOW: self-updating
        }

        public static void Uninstall()
        {
            if (!IsBepInExInstalled()) {
                throw Err("Nothing to uninstall.");
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

            string patcherFilePath = Path.Combine(RwDir, "BepInEx", "patchers", "BepInEx.Partiality.Patcher.dll");
            if (!File.Exists(patcherFilePath)) {
                return false;
            }

            try {
                var bepInEx = AssemblyName.GetAssemblyName(Path.Combine(bepInExCoreDirectory, "BepInEx.dll"));
                return bepInEx.Name == "BepInEx" && bepInEx.Version >= new Version(5, 4, 5, 0);
            } catch (BadImageFormatException) {
                return false;
            } catch (FileNotFoundException) {
                return false;
            }
        }

        private static async Task InstallBepInEx()
        {
            string bepInExPath = Path.Combine(RwDir, "BepInEx");
            if (Directory.Exists(bepInExPath))
                Directory.Delete(bepInExPath, true);

            try {
                await GetStreamFromGitHubRepository(Organization, BepRepo)
                    .ContinueWith(t => DownloadWithProgressAndUnzip(t.Result, RwDir));
            } catch (AggregateException e) {
                throw e.Flatten();
            }

            Console.WriteLine("Successfully installed BepInEx.");
        }
    }
}
