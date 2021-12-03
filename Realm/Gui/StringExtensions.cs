namespace Realm.Gui;

static class StringExtensions
{
    // CREDIT: https://github.com/SlimeCubed/DevConsole/blob/b0fe50a8af03cf3e686fc4c6f644bd01adb2f8cc/DevConsole/StringEx.cs
    // Awesome string split function
    /// <summary>
    /// Splits a string so that each segment's width is below <paramref name="maxWidth"/>.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="maxWidth">The upper bound for line width.</param>
    /// <param name="font">The font used to measure splitting.</param>
    /// <returns>String segments that will be under <paramref name="maxWidth"/> when displayed using <paramref name="font"/>.</returns>
    public static IEnumerable<string> SplitLongLines(this string text, float maxWidth, FFont font)
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

            // Advance based on kerning
            if (i == 0)
                x = -cInfo.offsetX;
            else
                x += kInfo.amount + font._textParams.scaledKerningOffset;

            x += cInfo.xadvance;

            lastChar = c;
        }

        return x;
    }

    /// <summary>
    /// Calculates the width, in pixels, of a string using <paramref name="font"/>.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font used to measure length.</param>
    /// <returns></returns>
    public static float MeasureWidth(this string text, string font)
    {
        return text.MeasureWidth(Futile.atlasManager.GetFontWithName(font) ?? throw new ArgumentException("No font.", nameof(font)));
    }
}
