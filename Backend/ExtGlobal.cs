namespace Backend;

static class ExtGlobal
{
    private static readonly List<Action> onExit = new();

    public static readonly string[] ModBlacklist = { "EnumExtender", "PublicityStunt", "AutoUpdate", "LogFix", "BepInEx-Partiality-Wrapper", "PartialityWrapper" };

    // Exceptions will be silently consumed.
    public static void OnExit(Action action) => onExit.Add(action);
    public static void Exit()
    {
        foreach (Action action in onExit) {
            try { action(); }
            catch { }
        }
    }
}
