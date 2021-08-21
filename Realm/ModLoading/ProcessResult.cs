using System.Diagnostics;
using System.IO;

namespace Realm.ModLoading
{
    public sealed class ProcessResult
    {
        public static ProcessResult From(string file, string args, int timeout = -1)
        {
            if (!File.Exists(file)) {
                throw new FileNotFoundException("Program not found.");
            }

            Process p = Process.Start(new ProcessStartInfo(file, args) {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false // Use CreateProcess, not ShellExecute, because we need the specific mutator file and we want to redirect stderr,
            });

            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();

            if (!p.WaitForExit(timeout)) {
                p.Kill();
                return new(null, output, error);
            }

            return new(p.ExitCode, output, error);
        }

        private ProcessResult(int? exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        // Null if timeout
        public int? ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
    }
}
