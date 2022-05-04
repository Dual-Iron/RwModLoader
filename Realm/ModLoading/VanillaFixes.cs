using UnityEngine;

namespace Realm.ModLoading;

static class VanillaFixes
{
    public static void Hook()
    {
        On.ProcessManager.SwitchMainProcess += ProcessManager_SwitchMainProcess;
        On.Options.Save += CreateUserDataDir;
        On.FFont.GetQuadInfoForText += FixFLabel;
    }

    private static void ProcessManager_SwitchMainProcess(On.ProcessManager.orig_SwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
    {
        try {
            orig(self, ID);
        }
        catch (Exception e) {
            Program.Logger.LogError($"There was an error while initializing MainLoopProcess {ID}! This normally freezes the game but it was caught because Realm.");
            Program.Logger.LogError(e.ToString());

            self.currentMainLoop = new Gui.Menus.ModMenu(self, e);
            self.blackFadeTime = self.currentMainLoop.FadeInTime;
            self.blackDelay = self.currentMainLoop.InitialBlackSeconds;
        }
    }

    private static void CreateUserDataDir(On.Options.orig_Save orig, Options self)
    {
        Directory.CreateDirectory(Path.Combine(RWCustom.Custom.RootFolderDirectory(), "UserData"));
        orig(self);
    }

    private static FLetterQuadLine[] FixFLabel(On.FFont.orig_GetQuadInfoForText orig, FFont self, string text, FTextParams labelTextParams)
    {
        return GetQuadInfoForText(self, text, labelTextParams);
    }

    private static FLetterQuadLine[] GetQuadInfoForText(FFont self, string text, FTextParams labelTextParams)
    {
        int letterCount = 0;

        char[] letters = text.ToCharArray();

        List<FLetterQuadLine> preLines = new(12);

        int lettersLength = letters.Length;
        for (int c = 0; c < lettersLength; ++c) {
            char letter = letters[c];

            if (letter == '\n') {
                preLines.Add(new FLetterQuadLine {
                    letterCount = letterCount,
                    quads = new FLetterQuad[letterCount]
                });

                letterCount = 0;
            }
            else {
                letterCount++;
            }
        }

        preLines.Add(new FLetterQuadLine {
            letterCount = letterCount,
            quads = new FLetterQuad[letterCount]
        });

        FLetterQuadLine[] lines = preLines.ToArray();

        int lineCount = 0;
        letterCount = 0;

        float nextX = 0;
        float nextY = 0;

        FCharInfo charInfo;

        char previousLetter = '\0';

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        float usableLineHeight = self._lineHeight + labelTextParams.scaledLineHeightOffset + self._textParams.scaledLineHeightOffset;

        for (int c = 0; c < lettersLength; ++c) {
            char letter = letters[c];

            if (letter == '\n') {
                if (letterCount == 0) {
                    lines[lineCount].bounds = new Rect(0, 0, nextY, nextY - usableLineHeight);
                }
                else {
                    lines[lineCount].bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
                }

                minX = float.MaxValue;
                maxX = float.MinValue;
                minY = float.MaxValue;
                maxY = float.MinValue;

                nextX = 0;
                nextY -= usableLineHeight;

                lineCount++;
                letterCount = 0;
            }
            else {
                FKerningInfo foundKerning = self._nullKerning;

                for (int k = 0; k < self._kerningCount; k++) {
                    FKerningInfo kerningInfo = self._kerningInfos[k];
                    if (kerningInfo.first == previousLetter && kerningInfo.second == letter) {
                        foundKerning = kerningInfo;
                    }
                }

                FLetterQuad letterQuad = new();

                if (self._charInfosByID.ContainsKey(letter)) {
                    charInfo = self._charInfosByID[letter];
                }
                else {
                    charInfo = self._charInfosByID[0];
                }

                float totalKern = foundKerning.amount + labelTextParams.scaledKerningOffset + self._textParams.scaledKerningOffset;

                if (letterCount == 0) {
                    nextX = -charInfo.offsetX;
                }
                else {
                    nextX += totalKern;
                }

                letterQuad.charInfo = charInfo;

                Rect quadRect = new(nextX + charInfo.offsetX, nextY - charInfo.offsetY - charInfo.height, charInfo.width, charInfo.height);

                letterQuad.rect = quadRect;

                lines[lineCount].quads[letterCount] = letterQuad;

                minX = Math.Min(minX, quadRect.xMin);
                maxX = Math.Max(maxX, quadRect.xMax);
                minY = Math.Min(minY, nextY - usableLineHeight);
                maxY = Math.Max(maxY, nextY);

                nextX += charInfo.xadvance;

                letterCount++;
            }

            previousLetter = letter;
        }

        if (letterCount == 0) {
            lines[lineCount].bounds = new Rect(0, 0, nextY, nextY - usableLineHeight);
        }
        else {
            lines[lineCount].bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        return lines;
    }
}
