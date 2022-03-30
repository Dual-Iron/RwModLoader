namespace Backend.Web;

static class ExtWeb
{
    private static HttpClient? client;

    public static HttpClient Client => client ??= new(new HttpClientHandler {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10
    });

    public static void DisposeClient()
    {
        client?.Dispose();
    }
}
