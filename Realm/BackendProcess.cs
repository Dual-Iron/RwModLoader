using System.Diagnostics;

namespace Realm;

sealed class BackendProcess
{
    public static Process Begin(string args)
    {
        if (!File.Exists(RealmPaths.BackendPath)) {
            throw new FileNotFoundException("Program not found.");
        }

        return Process.Start(new ProcessStartInfo(RealmPaths.BackendPath, args) {
            WorkingDirectory = RealmPaths.UserFolder.FullName,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false // Use CreateProcess, not ShellExecute, because we need the specific backend.exe file and we want to redirect stderr and stdout
        });
    }

    /// <summary>
    /// Waits for the process to die and kills it if it takes too long.
    /// </summary>
    /// <returns><see langword="false"/> if the process timed out; otherwise <see langword="true"/>.</returns>
    public static bool WaitKill(Process p, int timeout = -1)
    {
        if (!p.WaitForExit(timeout)) {
            p.Kill();
            return false;
        }
        return true;
    }

    public static BackendProcess Execute(string args, int timeout = -1)
    {
        using Process p = Begin(args);

        StringBuilder output = new();
        StringBuilder error = new();

        p.OutputDataReceived += (_, arg) => output.AppendLine(arg.Data);
        p.ErrorDataReceived += (_, arg) => error.AppendLine(arg.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        int? exitCode = WaitKill(p, timeout) ? p.ExitCode : null;

        return new(exitCode, output.ToString(), error.ToString());
    }

    private BackendProcess(int? exitCode, string output, string error)
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

    public override string ToString()
    {
        if (ExitCode == 0) {
            return $"Process completed successfully. {Output}";
        }
        if (ExitCode == null) {
            return "Process timed out.";
        }
        return $"Process exited with code {ExitCode}. {Error}";
    }
}
