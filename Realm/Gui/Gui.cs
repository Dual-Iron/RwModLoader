using Menu;
using Realm.Gui.Menus;

namespace Realm.Gui;

static class Gui
{
    public static void Hook()
    {
        ModMenuMusic.Hook();
        ModMenuHooks.Hook();

        if (State.DeveloperMode) {
            PauseMenuReload.Hook();
        }
    }

    public static FFont GetFont(string font)
    {
        return Futile.atlasManager.GetFontWithName(font) ?? throw new ArgumentException("No font.", nameof(font));
    }

    public static string JoinStr(this IEnumerable<string> strings, string join)
    {
        StringBuilder result = new();

        var enumerator = strings.GetEnumerator();
        if (enumerator.MoveNext()) {
            result.Append(enumerator.Current);
        }

        while (enumerator.MoveNext()) {
            result.Append(join);
            result.Append(enumerator.Current);
        }

        return result.ToString();
    }

    public static string JoinStrEnglish(this IEnumerable<string> strings)
    {
        List<string> strs = strings.ToList();

        return strs.Count switch {
            0 => "",
            1 => strs[0],
            2 => $"{strs[0]} and {strs[1]}",
            _ => JoinStrLong()
        };

        string JoinStrLong()
        {
            StringBuilder ret = new();
            for (int i = 0; i < strs.Count - 1; i++) {
                ret.Append(strs[i]);
                ret.Append(", ");
            }
            ret.Append("and ");
            ret.Append(strs.Last());
            return ret.ToString();
        }
    }

    // CREDIT: https://github.com/SlimeCubed/DevConsole/blob/b0fe50a8af03cf3e686fc4c6f644bd01adb2f8cc/DevConsole/StringEx.cs
    // Awesome string split function
    // Seriously this function is an MVP
    /// <summary>Splits a string so that each segment's width is below <paramref name="maxWidth"/>.</summary>
    public static IEnumerable<string> SplitLongLines(this string text, FFont font, float maxWidth)
    {
        int sliceStart = 0;
        int lastWhitespace = 0;
        char lastChar = '\0';
        float x = 0f;

        for (int i = 0; i < text.Length; i++) {
            char c = text[i];

            // Get char info
            if (!font._charInfosByID.TryGetValue(c, out FCharInfo? cInfo)) {
                cInfo = font._charInfosByID[0u];
            }

            // Find kerning offset
            FKerningInfo kInfo = font._nullKerning;
            for (int j = 0; j < font._kerningCount; j++) {
                FKerningInfo? candidate = font._kerningInfos[j];
                if (candidate.first == lastChar && candidate.second == c)
                    kInfo = candidate;
            }

            // Advance based on kerning
            if (i == sliceStart)
                x = -cInfo.offsetX;
            else
                x += kInfo.amount + font._textParams.scaledKerningOffset;

            if (c == '\n') {
                yield return text.Substring(sliceStart, i - sliceStart);

                sliceStart = i + 1;
                lastWhitespace = i + 1;
                x = 0;
            }
            else if (char.IsWhiteSpace(c)) {
                // Never split on whitespace
                lastWhitespace = i;

                x += cInfo.xadvance;
            }
            else {
                // Split if this char would go over the edge
                if (x + cInfo.width > maxWidth) {
                    int sliceEnd = sliceStart == lastWhitespace ? i : lastWhitespace + 1;

                    yield return text.Substring(sliceStart, sliceEnd - sliceStart);

                    sliceStart = sliceEnd;
                    lastWhitespace = sliceEnd;
                    i = sliceStart;
                    x = 0;
                }
                else
                    x += cInfo.xadvance;
            }

            lastChar = c;
        }

        yield return text.Substring(sliceStart);
    }

    /// <summary>Calculates the width, in pixels, of a string using <paramref name="font"/>. Ignores newlines.</summary>
    public static float MeasureWidth(this string text, FFont font)
    {
        char lastChar = '\0';
        float x = 0f;

        for (int i = 0; i < text.Length; i++) {
            char c = text[i];

            // Get char info
            if (!font._charInfosByID.TryGetValue(c, out FCharInfo? cInfo)) {
                cInfo = font._charInfosByID[0u];
            }

            // Find kerning offset
            FKerningInfo kInfo = font._nullKerning;
            for (int j = 0; j < font._kerningCount; j++) {
                FKerningInfo? candidate = font._kerningInfos[j];
                if (candidate.first == lastChar && candidate.second == c)
                    kInfo = candidate;
            }

            lastChar = c;

            // Advance based on kerning
            if (i == 0)
                x = -cInfo.offsetX;
            else
                x += kInfo.amount + font._textParams.scaledKerningOffset;

            x += cInfo.xadvance;
        }

        return x;
    }

    /// <summary>Culls text if it's too long. Ignores newlines.</summary>
    public static string CullLong(this string text, string font, float maxWidth, string ellipses = "...")
    {
        return text.CullLong(GetFont(font), maxWidth, ellipses);
    }

    /// <summary>Culls text if it's too long. Ignores newlines.</summary>
    public static string CullLong(this string text, FFont font, float maxWidth, string ellipses = "...")
    {
        float ellipsesWidth = ellipses.MeasureWidth(font);

        var preEllipses = -1;
        var lastChar = '\0';
        var x = 0f;

        for (int i = 0; i < text.Length; i++) {
            char c = text[i];

            // Get char info
            if (!font._charInfosByID.TryGetValue(c, out FCharInfo? cInfo)) {
                cInfo = font._charInfosByID[0u];
            }

            // Find kerning offset
            FKerningInfo kInfo = font._nullKerning;
            for (int j = 0; j < font._kerningCount; j++) {
                FKerningInfo? candidate = font._kerningInfos[j];
                if (candidate.first == lastChar && candidate.second == c)
                    kInfo = candidate;
            }

            lastChar = c;

            // Advance based on kerning
            if (i == 0)
                x = -cInfo.offsetX;
            else
                x += kInfo.amount + font._textParams.scaledKerningOffset;

            x += cInfo.xadvance;

            if (x <= maxWidth - ellipsesWidth) {
                preEllipses = i;
            }

            if (x > maxWidth) {
                return text.Substring(0, preEllipses + 1) + ellipses;
            }
        }

        return text;
    }

    /// <summary>Splits lines up until a certain number of rows.</summary>
    public static List<string> SplitLinesAndCull(string text, float width, int rows, string ellipses = "...")
    {
        var descStr = text.SplitLongLines(GetFont("font"), width).ToList();
        if (descStr.Count > rows) {
            // Append what's gonna be removed to the last line, then cull that last line.
            descStr[rows - 1] += descStr.GetRange(rows, descStr.Count - rows).JoinStr(" ");
            descStr[rows - 1] = descStr[rows - 1].CullLong("font", width, ellipses);

            // Remove extra lines.
            descStr.RemoveRange(rows, descStr.Count - rows);
        }
        return descStr;
    }

    /// <summary>Equivalent to <see cref="string.TrimEnd(char[])"/> but takes a predicate.</summary>
    public static string TrimEnd(this string s, Predicate<char> charFn)
    {
        return s.Substring(0, 1 + s.LastIndex(c => !charFn(c)));
    }

    /// <summary>Equivalent to <see cref="string.LastIndexOf(char)"/> but takes a predicate.</summary>
    public static int LastIndex(this string s, Predicate<char> charFn)
    {
        for (int i = s.Length - 1; i >= 0; i--) {
            if (charFn(s[i])) {
                return i;
            }
        }
        return -1;
    }

    public static MenuLabel WithColor(this MenuLabel label, Menu.Menu.MenuColors color)
    {
        label.label.color = Menu.Menu.MenuRGB(color);
        return label;
    }

    public static MenuLabel WithAlignment(this MenuLabel label, FLabelAlignment horizontalAlignment)
    {
        label.label.alignment = horizontalAlignment;
        return label;
    }

    public static void ClearSubObjects(this MenuObject menuObject)
    {
        foreach (var subObj in menuObject.subObjects) {
            subObj.RemoveSprites();
            menuObject.RecursiveRemoveSelectables(subObj);
        }
        menuObject.subObjects.Clear();
    }

    public static IEnumerable<MenuObject> RecursiveSubObjects(this MenuObject menuObject)
    {
        foreach (var sob in menuObject.subObjects) {
            yield return sob;

            foreach (var ssob in sob.RecursiveSubObjects()) {
                yield return ssob;
            }
        }
    }

    public static string GetRelativeTime(DateTime time)
    {
        var now = DateTime.UtcNow;
        var delta = now - time;
        if (delta.TotalMinutes < 2) return $"just now";
        if (delta.TotalMinutes < 60) return $"{delta.TotalMinutes:0} minutes ago";
        if (delta.TotalHours < 24) return $"{delta.TotalHours:0} hours ago";
        if (delta.TotalDays < 30) return $"{delta.TotalDays:0} days ago";
        if (time.Year == now.Year) return $"{time:MMM}";
        return $"{time:MMM yyyy}";
    }
}
