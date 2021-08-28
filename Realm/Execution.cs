using System.Diagnostics;
using System.IO;

namespace Realm
{
    public sealed class Execution
    {
        public static Process Begin(string file, string args)
        {
            if (!File.Exists(file)) {
                throw new FileNotFoundException("Program not found.");
            }

            return Process.Start(new ProcessStartInfo(file, args) {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false // Use CreateProcess, not ShellExecute, because we need the specific mutator file and we want to redirect stderr and stdout
            });
        }

        /// <summary>
        /// Waits for the process to die and kills it if it takes too long.
        /// </summary>
        /// <returns><see langword="false"/> if the process timed out; otherwise <see langword="true"/>.</returns>
        public static bool PolitelyKill(Process p, int timeout = -1)
        {
            if (!p.WaitForExit(timeout)) {
                p.Kill();
                return false;
            }
            return true;
        }

        public static Execution Run(string file, string args, int timeout = -1)
        {
            using Process p = Begin(file, args);

            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();

            return PolitelyKill(p, timeout) ? new(p.ExitCode, output, error) : new(null, output, error);
        }

        private Execution(int? exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        /// <summary>
        /// The process's exit code, or null if the process timed out.
        /// </summary>
        public int? ExitCode { get; }
        public string Output { get; }
        public string Error { get; }

        /// <summary>
        /// An exit message trimmed of punctuation. For example, "Process exited with code 0".
        /// </summary>
        public string ExitMessage => $"Process {(ExitCode == null ? "timed out" : "exited with code " + ExitCode)}";
    }
}
