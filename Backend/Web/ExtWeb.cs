namespace Backend.Web;

static class ExtWeb
{
    public static HttpClient Client => client ??= GetClient();

    private static HttpClient? client;

    private static HttpClient GetClient()
    {
        HttpClient client = new(new HttpClientHandler {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });
        ExtGlobal.OnExit(client.Dispose);
        return client;
    }
}
