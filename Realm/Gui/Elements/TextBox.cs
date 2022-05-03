using static Menu.Menu;
using static UnityEngine.Mathf;
using Menu;
using UnityEngine;
using System.Collections;

namespace Realm.Gui.Elements;

sealed class TextBox : RectangularMenuObject, SelectableMenuObject, ButtonMenuObject, IHoverable
{
    public struct Settings
    {
        public bool Big;
        public int Rows;
        public float Width; // excludes padding
        public string? Placeholder;
        public bool Rect;
        public Action<string>? OnInsert;

        private FFont? font;

        public FFont Font => font ??= (Big ? Gui.GetFont("DisplayFont") : Gui.GetFont("font"));
    }

    public bool Selectable { get; set; } = true;

    public bool IsMouseOverMe =>
        menu.mousePosition.x > ScreenPos.x && menu.mousePosition.x < ScreenPos.x + size.x &&
        menu.mousePosition.y > ScreenPos.y && menu.mousePosition.y < ScreenPos.y + size.y;
    public bool CurrentlySelectableMouse => Selectable && !GetButtonBehavior.greyedOut;
    public bool CurrentlySelectableNonMouse => Selectable;

    public ButtonBehavior GetButtonBehavior { get; }

    public bool InFocus => !GetButtonBehavior.greyedOut && Selectable && textBoxMenu.FocusedOn == this;

    readonly ITextBoxMenu textBoxMenu;
    readonly RoundedRect? expandingRect;
    readonly MenuLabel placeholder;
    readonly MenuLabel textHolder;
    readonly FSprite caret;
    readonly Settings settings;

    public readonly StringBuilder Text = new();

    Vector2 caretPos;
    bool shutdown;
    float timer;

    const float paddingWidth = 8;
    const float paddingHeight = 4;

    public TextBox(MenuObject owner, Vector2 pos, Settings settings, ITextBoxMenu? textBoxMenu = null)
        : base(owner.menu, owner, pos, size: new(
            x: settings.Width + paddingWidth * 2,
            y: settings.Rows * settings.Font._lineHeight + paddingHeight * 2 + 2 // + 2 height is to account for bottom pixels of rectangle
              ))
    {
        owner.Container.AddChild(Container = new());

        this.settings = settings;
        this.textBoxMenu = textBoxMenu ?? (ITextBoxMenu)menu;

        GetButtonBehavior = new(this);

        if (settings.Rect) {
            subObjects.Add(expandingRect = new RoundedRect(menu, this, default, size, true));
        }

        subObjects.Add(textHolder = new MenuLabel(menu, this, "", new(paddingWidth, size.y - paddingHeight), default, settings.Big));
        textHolder.label.anchorY = 1;
        textHolder.label.alignment = FLabelAlignment.Left;

        subObjects.Add(placeholder = new MenuLabel(menu, this, settings.Placeholder ?? "", new(paddingWidth, size.y - paddingHeight), default, settings.Big));
        placeholder.label.anchorY = 1;
        placeholder.label.alignment = FLabelAlignment.Left;

        Container.AddChild(caret = new("pixel") {
            scaleX = 2,
            scaleY = settings.Font._lineHeight - 2,
            isVisible = false,
            anchorX = 0,
            anchorY = 1,
        });

        SetCaretPos(0, 1);

        menu.manager.rainWorld.StartCoroutine(GetText());
    }

    public override void RemoveSprites()
    {
        base.RemoveSprites();
        caret.RemoveFromContainer();
        shutdown = true;
    }

    public override void Update()
    {
        base.Update();

        GetButtonBehavior.Update();

        timer += 1 / 40f;

        placeholder.label.isVisible = Text.Length == 0;

        // Update rectangle behavior
        if (expandingRect != null) {
            expandingRect.fillAlpha = Lerp(.3f, .6f, GetButtonBehavior.col);
            expandingRect.addSize = new Vector2(8, 4) * GetButtonBehavior.sizeBump;
        }

        // Update focus
        if (InFocus) {
            bool escapeKey = Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.Escape);
            bool escapeMouse = menu.mouseDown && !new Rect(ScreenPos.x, ScreenPos.y, size.x, size.y).Contains(menu.mousePosition);
            if (escapeKey || escapeMouse) {
                textBoxMenu.FocusedOn = null;
            }

            if (!Input.GetKey(KeyCode.Tab)) {
                return;
            }

            // Select the next text box, sorted vertically
            var next = owner.subObjects.OfType<TextBox>().Where(t => t.pos.y < pos.y).OrderBy(t => pos.y - t.pos.y).FirstOrDefault();
            if (next != null) {
                menu.selectedObject = next;
                menu.PlaySound(SoundID.MENU_Button_Select_Gamepad_Or_Keyboard);
            }
            else {
                // We're at the bottom, so select the first text box.
                var first = owner.subObjects.OfType<TextBox>().OrderByDescending(t => t.pos.y).FirstOrDefault();
                if (first != null && first != this) {
                    menu.selectedObject = next;
                    menu.PlaySound(SoundID.MENU_Button_Select_Gamepad_Or_Keyboard);
                }
            }
        }
        // If greyed out or not selectable, but the user is focused on the text box, unfocus the text box.
        else if (textBoxMenu.FocusedOn == this) {
            textBoxMenu.FocusedOn = null;
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        float flash = Lerp(GetButtonBehavior.lastFlash, GetButtonBehavior.flash, timeStacker);
        float col = Lerp(GetButtonBehavior.col, GetButtonBehavior.col, timeStacker);

        if (expandingRect != null) {
            Color outerColor = Color.Lerp(MenuRGB(MenuColors.MediumGrey), MenuRGB(MenuColors.White), Max(flash, col));
            for (int i = 0; i < 4; i++) {
                expandingRect.sprites[expandingRect.SideSprite(i)].color = outerColor;
                expandingRect.sprites[expandingRect.CornerSprite(i)].color = outerColor;
            }
        }

        placeholder.label.color = Color.Lerp(MenuRGB(MenuColors.DarkGrey), MenuRGB(MenuColors.MediumGrey), col);
        placeholder.label.alpha = Lerp(.5f, .65f, GetButtonBehavior.col);
        textHolder.label.alpha = Lerp(.65f, 1f, GetButtonBehavior.col);

        caret.SetPosition(ScreenPos + caretPos);
        caret.isVisible = InFocus;

        if (InFocus) {
            caret.alpha = (Sin(timer * PI * 2) + 1) / 2;
        }
    }

    public void Clicked()
    {
        textBoxMenu.FocusedOn = this;
    }

    public string? GetHoverInfo(MenuObject selected)
    {
        return settings.Placeholder;
    }

    private IEnumerator GetText()
    {
        while (!shutdown) {
            // Have to perform this logic in an enumerator because
            // enumerators run at native FPS unlike the rest of the game.
            if (InFocus) {
                string input = Input.inputString;

                if (input.Length > 0) {
                    try {
                        UpdateText(input);
                    }
                    catch (Exception e) {
                        Program.Logger.LogError(e);
                    }
                }
            }
            yield return null;
        }
    }

    private void UpdateText(string input)
    {
        foreach (char c in input) {
            string textOrig = Text.ToString();
            
            if (c == '\b') {
                // Backspace deletes last character
                if (Text.Length > 0)
                    Text.Remove(Text.Length - 1, 1);
            }
            else if (c == '\u007f') {
                static bool isSeparator(char c) => !char.IsLetterOrDigit(c);

                // Ctrl+Backspace deletes until reaching previous separator, or the whole string
                // Pretty much imitates Google Chrome
                int i = textOrig.TrimEnd(isSeparator).LastIndex(isSeparator);

                // No length check needed because `i` will be -1 if there were no matches. Convenient :)
                Text.Remove(i + 1, Text.Length - (i + 1));
            }
            else if (c == '\n' || c == '\r') {
                // Enter/Return creates a new line if there's enough room
                int lines = textOrig.SplitLongLines(settings.Font, size.x - paddingWidth * 2).Count();
                if (lines < settings.Rows)
                    Text.Append('\n');
            }
            else {
                // Allow appending new chars if (a) there are more lines anyway, or (b) there's enough room on the current (last) line
                var lines = textOrig.SplitLongLines(settings.Font, size.x - paddingWidth * 2).ToList();
                if (lines.Count < settings.Rows || $"{lines.Last()}{c}".MeasureWidth(settings.Font) < size.x - paddingWidth * 2) {
                    Text.Append(c);
                }
            }
        }

        string textFinal = Text.ToString();

        // Alert listeners that text has updated
        settings.OnInsert?.Invoke(textFinal);

        // Set text graphically
        var splitLines = textFinal.SplitLongLines(settings.Font, size.x - paddingWidth * 2).ToList();

        textHolder.text = splitLines.JoinStr("\n");

        SetCaretPos(splitLines.Last().MeasureWidth(settings.Font), splitLines.Count);
    }

    private void SetCaretPos(float lastLineWidth, int lines)
    {
        caretPos = new(
            x: paddingWidth + lastLineWidth,
            y: size.y - paddingHeight - 1 - (lines - 1) * settings.Font._lineHeight
            );
    }
}
