global using static Mutator.InstallerApi;
using System.Diagnostics;

namespace Mutator
{
    public static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0) {
                Console.WriteLine("Unexpected args.");
                return 1;
            }

            try {
                Installer
                    .SelfUpdate(new List<string> { "--uninstall", "--install" }.GetEnumerator())
                    .Wait();

                Console.Write("Installed the newest release of Realm. Press ENTER to start Rain World or any other key to exit. ");

                if (Console.ReadKey(true).Key == ConsoleKey.Enter) {
                    using var p = Process.Start(new ProcessStartInfo {
                        FileName = "steam://run/312520",
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }

                return 0;
            } catch (Exception e) {
                Console.WriteLine("Failure: " + e.Message);
                return 1;
            }
        }
    }
}