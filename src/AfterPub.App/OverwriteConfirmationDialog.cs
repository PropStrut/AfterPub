namespace AfterPub.App;

/// <summary>The choice a person makes when some selected files already have outputs.</summary>
public enum OverwriteChoice
{
    OverwriteAll,
    SkipThese,
    Cancel
}

/// <summary>
/// A small batch-level prompt: overwrite conflicting outputs, skip them, or cancel
/// the whole convert action. This is the "ask once, up front" half of CLAUDE.md
/// section 3.4. The more granular per-file Yes/No/Yes-to-all/No-to-all/Cancel flow
/// described alongside it is deferred — the same result is reachable today by
/// ticking fewer rows and converting in more than one pass.
/// </summary>
public sealed class OverwriteConfirmationDialog : Form
{
    public OverwriteChoice Choice { get; private set; } = OverwriteChoice.Cancel;

    public OverwriteConfirmationDialog(int conflictCount, int totalCount)
    {
        this.Text = "AfterPub";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.Width = 430;
        this.Height = 160;

        string fileWord = conflictCount == 1 ? "file" : "files";
        Label messageLabel = new Label
        {
            Text = $"{conflictCount} of the {totalCount} selected {fileWord} already have a PDF output.\nWhat would you like to do?",
            Left = 15,
            Top = 15,
            Width = 390,
            Height = 45
        };

        Button overwriteAllButton = new Button
        {
            Text = "Overwrite All",
            Left = 15,
            Top = 70,
            Width = 120
        };
        overwriteAllButton.Click += (_, _) =>
        {
            this.Choice = OverwriteChoice.OverwriteAll;
            this.Close();
        };

        Button skipTheseButton = new Button
        {
            Text = "Skip These",
            Left = 145,
            Top = 70,
            Width = 120
        };
        skipTheseButton.Click += (_, _) =>
        {
            this.Choice = OverwriteChoice.SkipThese;
            this.Close();
        };

        Button cancelButton = new Button
        {
            Text = "Cancel",
            Left = 275,
            Top = 70,
            Width = 120
        };
        cancelButton.Click += (_, _) =>
        {
            this.Choice = OverwriteChoice.Cancel;
            this.Close();
        };

        this.CancelButton = cancelButton;
        this.AcceptButton = overwriteAllButton;

        this.Controls.Add(messageLabel);
        this.Controls.Add(overwriteAllButton);
        this.Controls.Add(skipTheseButton);
        this.Controls.Add(cancelButton);
    }
}
