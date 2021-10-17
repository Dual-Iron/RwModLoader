using Menu;
using Realm.Jobs;
using Realm.Logging;
using Realm.Remote;
using System.Diagnostics;
using UnityEngine;

namespace Realm.Gui;

public sealed class RaindbPanel : RectangularMenuObject, IHoverable, IListable
{
    public const float Height = 131;
    public const int Width = 570;

    public RaindbPanel(RaindbMod raindbMod, MenuObject owner, Vector2 pos) : base(owner.menu, owner, pos, new(Width, Height))
    {
        const string ellipses = "...";
        const float buttonPadding = 34;

        var parentContainer = Container;
        parentContainer.AddChild(Container = new());

        RaindbMod = raindbMod;

        float posX = 0;

        // subObjects.Add(mod image, size: 180x125)
        subObjects.Add(new RoundedRect(menu, this, new(posX += 5, 3), new(posX += 180, 125), true));

        posX += 10;

        // Split name into lines, and move to the first line
        using var nameLinesEnum = raindbMod.Name.SplitLongLines(size.x - buttonPadding, "DisplayFont").GetEnumerator();
        _ = nameLinesEnum.MoveNext();

        // First line is the text we'll use
        string nameCulled = nameLinesEnum.Current;

        // If there's a line after the first line, then we need to cull the name (e.g. "EnumExtender" -> "EnumExte...")
        bool ellipsesRequired = nameLinesEnum.MoveNext();
        if (ellipsesRequired)
            nameCulled = nameCulled.Substring(0, nameCulled.Length - 3) + ellipses;

        // Add label
        float nameWidth = nameCulled.MeasureWidth("DisplayFont");
        subObjects.Add(new MenuLabel(menu, this, nameCulled, new(posX, 120), new(nameWidth, 0), true));

        // Ensure we have an author and that we have space to put the author label
        if (!ellipsesRequired && !string.IsNullOrEmpty(raindbMod.Author)) {
            // Check rigorously that the author label fits
            string authorString = $"by {raindbMod.Author}";
            float x = posX + nameWidth + 8;
            if (x + authorString.MeasureWidth("font") < size.x - buttonPadding) {
                MenuLabel item = new(menu, this, authorString, new(x, 120 - 3), default, false);
                item.label.alignment = FLabelAlignment.Left;
                subObjects.Add(item);
            }
        }

        // Add a description. Pretty self-explanatory.
        var descriptionLines = raindbMod.Description.SplitLongLines(size.x - posX - buttonPadding, "font");

        int lineNumber = 0;
        foreach (var line in descriptionLines) {
            MenuLabel label = new(menu, this, line, new(posX, 90 - lineNumber * 20), default, false);
            label.label.alignment = FLabelAlignment.Left;
            subObjects.Add(label);

            lineNumber++;
        }

        //subObjects.Add(linkButton = new SymbolButton(menu, this, "Menu_Symbol_Clear_All", "", new(size.x - 29, size.y - 53)));

        subObjects.Add(dnldButton = new SymbolButton(menu, this, "Menu_Symbol_Clear_All", "", new(size.x - 29, size.y - 24)));
        downloaded = State.Instance.CurrentRefreshCache.Headers.Any(rwmf => rwmf.Name == raindbMod.Name);

        subObjects.Add(progDisplayContainer = new(this));
        progDisplayContainer.subObjects.Add(new ProgressableDisplay(performingProgress, progDisplayContainer, default, size, true));
    }

    private readonly SymbolButton? linkButton;
    private readonly SymbolButton dnldButton;

    private readonly MenuContainer progDisplayContainer;
    private readonly LoggingProgressable performingProgress = new();

    private bool downloaded;

    public bool IsBelow { get; set; }
    public bool BlockInteraction { get; set; }
    public float Visibility { get; set; }

    public Vector2 Pos { set => pos = value; }
    public Vector2 Size => size;
    public bool PreventButtonClicks => menu.manager.upcomingProcess != null || performingJob != null;

    public RaindbMod RaindbMod { get; }

    private Job? performingJob;

    public override void Update()
    {
        if (performingJob?.Status == JobStatus.Finished) {
            performingJob = null;
        }

        foreach (var sob in subObjects) {
            if (sob is ButtonTemplate button) {
                button.GetButtonBehavior.greyedOut = BlockInteraction || PreventButtonClicks;
            }
        }

        if (downloaded) {
            dnldButton.GetButtonBehavior.greyedOut = true;
        }

        base.Update();
    }

    public override void GrafUpdate(float timeStacker)
    {
        progDisplayContainer.Container.alpha = performingJob != null ? 1 : 0;
        Container.alpha = Visibility * Visibility;

        base.GrafUpdate(timeStacker);
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (sender == linkButton) {
            OpenHomepage();
        } else if (sender == dnldButton && !downloaded) {
            downloaded = true;
            performingJob = Job.Start(() => {
                if (!RaindbMod.IsGitHub) {
                    downloaded = false;
                    OpenHomepage();
                    return;
                }

                performingProgress.Message(MessageType.Info, "Downloading");

                Execution pr = Execution.Run(RealmPaths.MutatorPath, $"--download \"{RaindbMod.Author}/{RaindbMod.Name}\"");

                if (pr.ExitCode == 12 /* RepoNotCompliant */) {
                    downloaded = false;
                    OpenHomepage();
                } else if (pr.ExitCode != 0) {
                    performingProgress.Message(MessageType.Diagnostic, pr.Error);
                    performingProgress.Message(MessageType.Fatal, $"{pr.ExitMessage}: {pr.Output}");
                }
            });
        }
    }

    private void OpenHomepage()
    {
        Process.Start(new ProcessStartInfo(RaindbMod.HomepageUrl) { UseShellExecute = true }).Dispose();
    }

    string IHoverable.GetHoverInfo(MenuObject selected)
    {
        if (selected == linkButton) {
            return "Open mod page";
        }
        if (selected == dnldButton) {
            return "Download mod";
        }
        return "";
    }
}
