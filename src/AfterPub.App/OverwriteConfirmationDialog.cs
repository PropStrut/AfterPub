using AfterPub.Core.Scanning;

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
///
/// The message names each format that already has files (PDF, ODG and/or SLA) and how many,
/// so it is clear exactly which existing files would be replaced.
/// </summary>
public sealed class OverwriteConfirmationDialog : Form
{
    private const int ContentWidth = 390;

    public OverwriteChoice Choice { get; private set; } = OverwriteChoice.Cancel;

    /// <param name="conflictCount">How many selected files already have at least one of the chosen outputs.</param>
    /// <param name="totalCount">How many files are selected.</param>
    /// <param name="formatCounts">For each format being converted to that already has files, how many.</param>
    public OverwriteConfirmationDialog(
        int conflictCount,
        int totalCount,
        IReadOnlyList<(OutputTarget Target, int Count)> formatCounts)
    {
        this.Text = "AfterPub";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;

        string fileWord = conflictCount == 1 ? "file" : "files";
        string existingList = string.Join(
            Environment.NewLine,
            formatCounts.Select(item => $"    {item.Target.ToString().ToUpperInvariant()}: {item.Count} {(item.Count == 1 ? "file" : "files")}"));

        Label messageLabel = new Label
        {
            Text = $"{conflictCount} of the {totalCount} selected {fileWord} already have output in a format you are converting to:"
                + Environment.NewLine + Environment.NewLine
                + existingList
                + Environment.NewLine + Environment.NewLine
                + "Overwrite All replaces those existing files. Skip These leaves them alone and skips those files entirely.",
            Left = 15,
            Top = 15,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0)
        };

        int buttonTop = 15 + messageLabel.GetPreferredSize(new Size(ContentWidth, 0)).Height + 15;
        this.ClientSize = new Size(430, buttonTop + 40);

        Button overwriteAllButton = new Button
        {
            Text = "Overwrite All",
            Left = 15,
            Top = buttonTop,
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
            Top = buttonTop,
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
            Top = buttonTop,
            Width = 120
        };
        cancelButton.Click += (_, _) =>
        {
            this.Choice = OverwriteChoice.Cancel;
            this.Close();
        };

        this.CancelButton = cancelButton;

        // Pressing Enter takes the choice that destroys nothing.
        this.AcceptButton = skipTheseButton;

        this.Controls.Add(messageLabel);
        this.Controls.Add(overwriteAllButton);
        this.Controls.Add(skipTheseButton);
        this.Controls.Add(cancelButton);
    }
}
