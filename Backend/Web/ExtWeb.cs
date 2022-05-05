namespace Backend.Web;

static class ExtWeb
{
    public static HttpClient Client => client ??= GetClient();

    private static HttpClient? client;

    private static HttpClient GetClient()
    {
        HttpClient client = new(new HttpClientHandler {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        });
        ExtGlobal.OnExit(client.Dispose);
        return client;
    }

    public static async Task CopyToWithProgress(this Stream input, Stream output, long length, Action<long, long> onProgressChange)
    {
        const int bufferSize = 81920;

        long progressCurrent = 0;

        onProgressChange(0, length);

        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0) {
            progressCurrent += bytesRead;
            onProgressChange(progressCurrent, length);

            await output.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
        }
    }

    public static void PrintProgress(long current, long max)
    {
        Console.WriteLine($"PROGRESS: {current}/{max}");
    }
}
