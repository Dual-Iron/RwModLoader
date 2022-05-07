using Realm.Logging;

namespace Realm.Api;

public static class Mods
{
    /// <summary>The mod menu's process ID. Can be used with <see cref="ProcessManager.SwitchMainProcess(ProcessManager.ProcessID)"/> to switch to the mod menu.</summary>
    public const ProcessManager.ProcessID ModMenu = Gui.Menus.ModMenu.ModMenuID;

    /// <summary>Returns <see langword="true"/> if the mod with the given name is enabled.</summary>
    public static bool IsEnabled(string mod)
    {
        return State.Prefs.EnabledMods.Contains(mod);
    }

    /// <summary>Enables a set of mods, then saves changes. Each string should be the mod's name, like <c>"CentiShields"</c>.</summary>
    public static void Enable(params string[] mods)
    {
        State.Prefs.Enable(mods);
        State.Prefs.Save();
    }

    /// <summary>Disables a set of mods, then saves changes. Each string should be the mod's name, like <c>"CentiShields"</c>.</summary>
    public static void Disable(params string[] mods)
    {
        State.Prefs.Disable(mods);
        State.Prefs.Save();
    }

    /// <summary>Reloads every mod.</summary>
    public static void ReloadAll(IProgressTracker progressTracker)
    {
        CallbackProgressable progressable = new((p)    => progressTracker.ProgressUpdate(p),
                                                (m, s) => progressTracker.MessageUpdate((MessageSeverity)(int)m, s));
        State.Mods.Reload(progressable);
    }
}

/// <summary>Receives progress updates from a long-running task like reloading mods.</summary>
public interface IProgressTracker
{
    /// <summary>Called when progress increases or decreases. The <paramref name="progress"/> will be between 0 and 1, inclusive.</summary>
    void ProgressUpdate(float progress);
    /// <summary>Called when a message is sent to the progressable.</summary>
    void MessageUpdate(MessageSeverity severity, string contents);
}

/// <summary>The severity of a message.</summary>
public enum MessageSeverity
{
    /// <summary>The message helps developers debug.</summary>
    Debug,
    /// <summary>The message informs users.</summary>
    Info,
    /// <summary>The message warns that something might have gone wrong.</summary>
    Warning,
    /// <summary>The message declares a fatal error has occurred.</summary>
    Fatal
}
