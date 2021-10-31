namespace Mutator.Web;

static class ExtWeb
{
    private static HttpClient? client;

    public static HttpClient Client => client ??= new();

    public static void DisposeClient()
    {
        client?.Dispose();
    }
}
