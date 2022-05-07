using Realm.Threading;

namespace Realm.Gui;

public enum AsyncDownloadStatus { Unstarted, Downloading, Success, Errored }

sealed class AsyncDownload
{
    private readonly string backendArgs;
    private string? error;

    /// <summary>This will be called from the worker thread. Beware.</summary>
    public event Action? OnFinish;
    public event Action<long, long>? OnProgressUpdate;

    public AsyncDownload(string backendArgs)
    {
        this.backendArgs = backendArgs;
    }

    public AsyncDownloadStatus Status { get; private set; }

    public void Start()
    {
        if (Status == AsyncDownloadStatus.Unstarted) {
            Status = AsyncDownloadStatus.Downloading;

            NetworkThread.Instance.Enqueue(() => {
                Status = Download();
                OnFinish?.Invoke();
            });
        }
    }

    private AsyncDownloadStatus Download()
    {
        var proc = BackendProcess.Begin(backendArgs);

        while (proc.StandardOutput.ReadLine() is string line) {
            if (line.StartsWith("PROGRESS: ")) {
                string[] split = line.Substring("PROGRESS: ".Length).Split('/');

                if (split.Length == 2 && long.TryParse(split[0], out long current) && long.TryParse(split[1], out long max)) {
                    OnProgressUpdate?.Invoke(current, max);
                }
            }
        }

        string? error = proc.StandardError.ReadToEnd();

        if (proc.ExitCode == 0) {
            return AsyncDownloadStatus.Success;
        }
        else {
            Program.Logger.LogError($"Failed to download with error {proc.ExitCode}." +
                $"\n  ARGS   {backendArgs}" +
                $"\n  ERROR  {error}"
                );

            this.error = proc.ExitCode == 0x31 // Connection failed
                ? "Connection failed. Do you have internet access?"
                : error;

            return AsyncDownloadStatus.Errored;
        }
    }

    public override string ToString()
    {
        return Status switch {
            AsyncDownloadStatus.Unstarted => "Download not started yet",
            AsyncDownloadStatus.Downloading => "Download in progress",
            AsyncDownloadStatus.Success => "Download successful",
            AsyncDownloadStatus.Errored => error ?? "Unknown error occurred",
            _ => "???"
        };
    }
}
