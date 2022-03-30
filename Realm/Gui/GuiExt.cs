using Menu;
using UnityEngine;

namespace Realm.Gui;

static class GuiExt
{
    public static Vector2 ScreenSize => new(1366f, 768f);

    /// <summary>
    /// Gets the font with the specified name.
    /// </summary>
    public static FFont GetFont(string font)
    {
        return Futile.atlasManager.GetFontWithName(font) ?? throw new ArgumentException("No font.", nameof(font));
    }
    /// <summary>
    /// Join a string sequence. Because I'm tired of using this <see cref="string.Join(string, string[])"/> junk.
    /// </summary>
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

    /// <summary>
    /// Splits a string so that each segment's width is below <paramref name="maxWidth"/>.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="font">The font used to measure text width.</param>
    /// <param name="maxWidth">The upper bound for line width.</param>
    /// <returns>String segments that will be under <paramref name="maxWidth"/> when displayed using <paramref name="font"/>.</returns>
    public static IEnumerable<string> SplitLongLines(this string text, string font, float maxWidth)
    {
        return text.SplitLongLines(GetFont(font), maxWidth);
    }

    // CREDIT: https://github.com/SlimeCubed/DevConsole/blob/b0fe50a8af03cf3e686fc4c6f644bd01adb2f8cc/DevConsole/StringEx.cs
    // Awesome string split function
    /// <summary>
    /// Splits a string so that each segment's width is below <paramref name="maxWidth"/>.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="font">The font used to measure text width.</param>
    /// <param name="maxWidth">The upper bound for line width.</param>
    /// <returns>String segments that will be under <paramref name="maxWidth"/> when displayed using <paramref name="font"/>.</returns>
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

            if (char.IsWhiteSpace(c)) {
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

    /// <summary>
    /// Calculates the width, in pixels, of a string using <paramref name="font"/>.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font used to measure length.</param>
    /// <returns></returns>
    public static float MeasureWidth(this string text, string font)
    {
        return text.MeasureWidth(GetFont(font));
    }

    /// <summary>
    /// Calculates the width, in pixels, of a string using <paramref name="font"/>.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font used to measure text width.</param>
    /// <returns></returns>
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

    /// <summary>
    /// Culls text if it's too long.
    /// </summary>
    /// <param name="text">The text to cull.</param>
    /// <param name="font">The font to measure text width.</param>
    /// <param name="maxWidth">The maximum width before the text is culled.</param>
    /// <param name="ellipses">If text is culled, this string is appended to the end of it.</param>
    public static string CullLong(this string text, string font, float maxWidth, string ellipses = "...")
    {
        return text.CullLong(GetFont(font), maxWidth, ellipses);
    }

    /// <summary>
    /// Culls text if it's too long.
    /// </summary>
    /// <param name="text">The text to cull.</param>
    /// <param name="font">The font to measure text width.</param>
    /// <param name="maxWidth">The maximum width before the text is culled.</param>
    /// <param name="ellipses">If text is culled, this string is appended to the end of it.</param>
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

    /// <summary>
    /// Splits lines up until a certain number of rows.
    /// </summary>
    public static List<string> SplitLinesAndCull(string text, float width, int rows)
    {
        var descStr = text.SplitLongLines("font", width).ToList();
        if (descStr.Count > rows) {
            // Append what's gonna be removed to the last line, then cull that last line.
            descStr[rows - 1] += descStr.GetRange(rows, descStr.Count - rows).JoinStr(" ");
            descStr[rows - 1] = descStr[rows - 1].CullLong("font", width);

            // Remove extra lines.
            descStr.RemoveRange(rows, descStr.Count - rows);
        }
        return descStr;
    }

    /// <summary>
    /// Clears all sub objects from a <see cref="MenuObject"/> and clears their sprites.
    /// </summary>
    public static void ClearSubObjects(this MenuObject menuObject)
    {
        foreach (var subObj in menuObject.subObjects) {
            subObj.RemoveSprites();
            menuObject.RecursiveRemoveSelectables(subObj);
        }
        menuObject.subObjects.Clear();
    }

    /// <summary>
    /// Gets all sub objects from a <see cref="MenuObject"/>.
    /// </summary>
    public static IEnumerable<MenuObject> RecursiveSubObjects(this MenuObject menuObject)
    {
        foreach (var sob in menuObject.subObjects) {
            yield return sob;

            foreach (var ssob in sob.RecursiveSubObjects()) {
                yield return ssob;
            }
        }
    }

    /// <summary>
    /// Gets a relative time string. God forbid I ever localize this.
    /// </summary>
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
