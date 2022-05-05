namespace Realm.Gui;

public enum AsyncDownloadStatus { Unstarted, Downloading, Success, Errored }

sealed class AsyncDownload
{
    private readonly string backendArgs;
    private readonly int timeout;
    private string? error;

    /// <summary>This will be called from the worker thread. Beware.</summary>
    public event Action? OnFinish;

    public AsyncDownload(string backendArgs, int timeout = -1)
    {
        this.backendArgs = backendArgs;
        this.timeout = timeout;
    }

    public AsyncDownloadStatus Status { get; private set; }

    public void Start()
    {
        if (Status == AsyncDownloadStatus.Unstarted) {
            Status = AsyncDownloadStatus.Downloading;
            Job.Start(() => {
                Status = Download();
                OnFinish?.Invoke();
            });
        }
    }

    private AsyncDownloadStatus Download()
    {
        var proc = BackendProcess.Execute(backendArgs, timeout);

        if (proc.ExitCode == 0) {
            return AsyncDownloadStatus.Success;
        }
        else if (proc.ExitCode == null) {
            Program.Logger.LogError($"Download timed out.");
            error = "Download timed out";
            return AsyncDownloadStatus.Errored;
        }
        else {
            Program.Logger.LogError($"Failed to download with error {proc.ExitCode}." +
                $"\n  ARGS   {backendArgs}" +
                $"\n  ERROR  {proc.Error}"
                );

            error = proc.ExitCode == 0x31 // Connection failed
                ? "Connection failed. Do you have internet access?"
                : proc.Error;

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
