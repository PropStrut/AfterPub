using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using AfterPub.Core.Settings;

namespace AfterPub.App;

/// <summary>
/// Minimal-but-real UI: pick a folder, choose output targets, location, engine, and
/// overwrite behavior, scan, tick the rows you want, convert them. No quality
/// options or record file yet (CLAUDE.md section 14).
/// </summary>
public class MainForm : Form
{
    private const string SelectColumnName = "Select";
    private const string PdfStatusColumnName = "PdfStatus";
    private const string OdgStatusColumnName = "OdgStatus";

    private readonly AppSettingsStore _settingsStore;
    private IReadOnlyList<ConversionRow> _lastScanRows = Array.Empty<ConversionRow>();

    private readonly TextBox _folderTextBox;
    private readonly Button _browseButton;
    private readonly CheckBox _recursiveCheckBox;
    private readonly Button _scanButton;

    private readonly GroupBox _outputLocationGroup;
    private readonly RadioButton _sameFolderRadio;
    private readonly RadioButton _separateFolderRadio;
    private readonly TextBox _separateFolderTextBox;
    private readonly Button _separateFolderBrowseButton;

    private readonly GroupBox _targetsGroup;
    private readonly CheckBox _pdfTargetCheckBox;
    private readonly CheckBox _odgTargetCheckBox;

    private readonly GroupBox _engineGroup;
    private readonly RadioButton _autoEngineRadio;
    private readonly RadioButton _publisherEngineRadio;
    private readonly RadioButton _libreOfficeEngineRadio;
    private readonly Label _libreOfficePathLabel;
    private readonly TextBox _libreOfficePathTextBox;
    private readonly Button _libreOfficePathBrowseButton;
    private readonly Label _engineStatusLabel;

    private readonly Button _convertButton;
    private readonly Label _overwriteLabel;
    private readonly RadioButton _overwriteAskRadio;
    private readonly RadioButton _overwriteSkipRadio;
    private readonly RadioButton _overwriteOverwriteRadio;
    private readonly DataGridView _resultsGrid;

    public MainForm()
    {
        this._settingsStore = new AppSettingsStore(AppSettingsStore.GetDefaultFilePath());

        this.Text = "AfterPub";
        this.Width = 900;
        this.Height = 780;
        this.MinimumSize = new Size(700, 680);

        // --- Row 1: source folder, recursive, scan ---

        this._folderTextBox = new TextBox
        {
            Left = 10,
            Top = 12,
            Width = 500,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._browseButton = new Button
        {
            Text = "Browse...",
            Left = 520,
            Top = 10,
            Width = 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._browseButton.Click += this.OnBrowseSourceClick;

        this._recursiveCheckBox = new CheckBox
        {
            Text = "Include subfolders",
            Left = 620,
            Top = 14,
            Width = 150,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        this._scanButton = new Button
        {
            Text = "Scan",
            Left = 780,
            Top = 10,
            Width = 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._scanButton.Click += this.OnScanClick;

        // --- Row 2: output location ---

        this._outputLocationGroup = new GroupBox
        {
            Text = "Output location",
            Left = 10,
            Top = 44,
            Width = 860,
            Height = 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._sameFolderRadio = new RadioButton
        {
            Text = "Same folder as the source .pub file",
            Left = 10,
            Top = 22,
            Width = 400,
            Checked = true
        };

        this._separateFolderRadio = new RadioButton
        {
            Text = "Separate folder:",
            Left = 10,
            Top = 50,
            Width = 130
        };
        this._separateFolderRadio.CheckedChanged += this.OnLocationModeChanged;

        this._separateFolderTextBox = new TextBox
        {
            Left = 150,
            Top = 48,
            Width = 590,
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._separateFolderBrowseButton = new Button
        {
            Text = "Browse...",
            Left = 750,
            Top = 46,
            Width = 90,
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._separateFolderBrowseButton.Click += this.OnBrowseSeparateFolderClick;

        this._outputLocationGroup.Controls.Add(this._sameFolderRadio);
        this._outputLocationGroup.Controls.Add(this._separateFolderRadio);
        this._outputLocationGroup.Controls.Add(this._separateFolderTextBox);
        this._outputLocationGroup.Controls.Add(this._separateFolderBrowseButton);

        // --- Row 3: output targets ---

        this._targetsGroup = new GroupBox
        {
            Text = "Output targets",
            Left = 10,
            Top = 144,
            Width = 860,
            Height = 54,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._pdfTargetCheckBox = new CheckBox
        {
            Text = "PDF",
            Left = 10,
            Top = 22,
            Width = 100,
            Checked = true
        };

        this._odgTargetCheckBox = new CheckBox
        {
            Text = "ODG (editable in LibreOffice Draw — needs LibreOffice)",
            Left = 120,
            Top = 22,
            Width = 400
        };

        this._targetsGroup.Controls.Add(this._pdfTargetCheckBox);
        this._targetsGroup.Controls.Add(this._odgTargetCheckBox);

        // --- Row 4: conversion engine ---

        this._engineGroup = new GroupBox
        {
            Text = "Conversion engine",
            Left = 10,
            Top = 208,
            Width = 860,
            Height = 120,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._autoEngineRadio = new RadioButton
        {
            Text = "Auto (Publisher if available, otherwise LibreOffice)",
            Left = 10,
            Top = 20,
            Width = 400,
            Checked = true
        };

        this._publisherEngineRadio = new RadioButton
        {
            Text = "Publisher",
            Left = 10,
            Top = 44,
            Width = 140
        };

        this._libreOfficeEngineRadio = new RadioButton
        {
            Text = "LibreOffice",
            Left = 160,
            Top = 44,
            Width = 140
        };

        this._libreOfficePathLabel = new Label
        {
            Text = "LibreOffice path (optional):",
            Left = 10,
            Top = 72,
            Width = 180
        };

        this._libreOfficePathTextBox = new TextBox
        {
            Left = 200,
            Top = 69,
            Width = 550,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._libreOfficePathBrowseButton = new Button
        {
            Text = "Browse...",
            Left = 760,
            Top = 67,
            Width = 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._libreOfficePathBrowseButton.Click += this.OnBrowseLibreOfficePathClick;

        this._engineStatusLabel = new Label
        {
            Left = 10,
            Top = 96,
            Width = 840,
            Height = 18,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._engineGroup.Controls.Add(this._autoEngineRadio);
        this._engineGroup.Controls.Add(this._publisherEngineRadio);
        this._engineGroup.Controls.Add(this._libreOfficeEngineRadio);
        this._engineGroup.Controls.Add(this._libreOfficePathLabel);
        this._engineGroup.Controls.Add(this._libreOfficePathTextBox);
        this._engineGroup.Controls.Add(this._libreOfficePathBrowseButton);
        this._engineGroup.Controls.Add(this._engineStatusLabel);

        // --- Row 5: convert button ---

        this._convertButton = new Button
        {
            Text = "Convert selected",
            Left = 10,
            Top = 338,
            Width = 160,
            Height = 26
        };
        this._convertButton.Click += this.OnConvertSelectedClick;

        this._overwriteLabel = new Label
        {
            Text = "If output exists:",
            Left = 185,
            Top = 343,
            Width = 100
        };

        this._overwriteAskRadio = new RadioButton
        {
            Text = "Ask",
            Left = 285,
            Top = 341,
            Width = 55,
            Checked = true
        };

        this._overwriteSkipRadio = new RadioButton
        {
            Text = "Skip",
            Left = 345,
            Top = 341,
            Width = 60
        };

        this._overwriteOverwriteRadio = new RadioButton
        {
            Text = "Overwrite",
            Left = 410,
            Top = 341,
            Width = 90
        };

        // --- Results grid ---

        this._resultsGrid = new DataGridView
        {
            Left = 10,
            Top = 374,
            Width = 860,
            Height = 330,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        DataGridViewCheckBoxColumn selectColumn = new DataGridViewCheckBoxColumn
        {
            Name = SelectColumnName,
            HeaderText = string.Empty,
            FillWeight = 20,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        this._resultsGrid.Columns.Add(selectColumn);
        this._resultsGrid.Columns.Add("SourceFolder", "Folder");
        this._resultsGrid.Columns.Add("FileName", "File Name");
        this._resultsGrid.Columns.Add(PdfStatusColumnName, "PDF");
        this._resultsGrid.Columns.Add(OdgStatusColumnName, "ODG");

        foreach (DataGridViewColumn column in this._resultsGrid.Columns)
        {
            if (column.Name != SelectColumnName)
            {
                column.ReadOnly = true;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }

        this.Controls.Add(this._folderTextBox);
        this.Controls.Add(this._browseButton);
        this.Controls.Add(this._recursiveCheckBox);
        this.Controls.Add(this._scanButton);
        this.Controls.Add(this._outputLocationGroup);
        this.Controls.Add(this._targetsGroup);
        this.Controls.Add(this._engineGroup);
        this.Controls.Add(this._convertButton);
        this.Controls.Add(this._overwriteLabel);
        this.Controls.Add(this._overwriteAskRadio);
        this.Controls.Add(this._overwriteSkipRadio);
        this.Controls.Add(this._overwriteOverwriteRadio);
        this.Controls.Add(this._resultsGrid);

        this.LoadSettingsIntoControls();
        this.RefreshEngineStatusLabel();
        this.FormClosing += this.OnFormClosing;
    }

    private void LoadSettingsIntoControls()
    {
        AppSettings settings = this._settingsStore.Load();

        this._recursiveCheckBox.Checked = settings.Recursive;

        if (settings.LocationMode == OutputLocationMode.SeparateFolder)
        {
            this._separateFolderRadio.Checked = true;
            this._separateFolderTextBox.Text = settings.SeparateOutputRoot ?? string.Empty;
        }
        else
        {
            this._sameFolderRadio.Checked = true;
        }

        this._pdfTargetCheckBox.Checked = settings.EnabledTargets.Contains(OutputTarget.Pdf);
        this._odgTargetCheckBox.Checked = settings.EnabledTargets.Contains(OutputTarget.Odg);

        switch (settings.EngineSelection)
        {
            case EngineSelection.Publisher:
                this._publisherEngineRadio.Checked = true;
                break;
            case EngineSelection.LibreOffice:
                this._libreOfficeEngineRadio.Checked = true;
                break;
            default:
                this._autoEngineRadio.Checked = true;
                break;
        }

        this._libreOfficePathTextBox.Text = settings.LibreOfficePath ?? string.Empty;

        switch (settings.OverwriteBehavior)
        {
            case OverwriteBehavior.Skip:
                this._overwriteSkipRadio.Checked = true;
                break;
            case OverwriteBehavior.Overwrite:
                this._overwriteOverwriteRadio.Checked = true;
                break;
            default:
                this._overwriteAskRadio.Checked = true;
                break;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        AppSettings settings = new AppSettings
        {
            Recursive = this._recursiveCheckBox.Checked,
            LocationMode = this._separateFolderRadio.Checked
                ? OutputLocationMode.SeparateFolder
                : OutputLocationMode.SameFolder,
            SeparateOutputRoot = this._separateFolderTextBox.Text.Trim(),
            EngineSelection = this.GetSelectedEngineSelection(),
            LibreOfficePath = this._libreOfficePathTextBox.Text.Trim(),
            OverwriteBehavior = this.GetSelectedOverwriteBehavior(),
            EnabledTargets = this.GetSelectedTargets().ToList()
        };

        this._settingsStore.Save(settings);
    }

    private List<OutputTarget> GetSelectedTargets()
    {
        List<OutputTarget> targets = new List<OutputTarget>();

        if (this._pdfTargetCheckBox.Checked)
        {
            targets.Add(OutputTarget.Pdf);
        }

        if (this._odgTargetCheckBox.Checked)
        {
            targets.Add(OutputTarget.Odg);
        }

        return targets;
    }

    private OverwriteBehavior GetSelectedOverwriteBehavior()
    {
        if (this._overwriteSkipRadio.Checked)
        {
            return OverwriteBehavior.Skip;
        }

        if (this._overwriteOverwriteRadio.Checked)
        {
            return OverwriteBehavior.Overwrite;
        }

        return OverwriteBehavior.Ask;
    }

    private EngineSelection GetSelectedEngineSelection()
    {
        if (this._publisherEngineRadio.Checked)
        {
            return EngineSelection.Publisher;
        }

        if (this._libreOfficeEngineRadio.Checked)
        {
            return EngineSelection.LibreOffice;
        }

        return EngineSelection.Auto;
    }

    private void RefreshEngineStatusLabel()
    {
        PublisherConversionEngine publisherEngine = new PublisherConversionEngine();
        LibreOfficeConversionEngine libreOfficeEngine = this.CreateLibreOfficeEngine();

        string publisherStatus = publisherEngine.IsAvailable() ? "found" : "not found";
        string libreOfficeStatus = libreOfficeEngine.ResolvedExecutablePath is string path
            ? $"found at {path}"
            : "not found";

        this._engineStatusLabel.Text = $"Detected — Publisher: {publisherStatus}    LibreOffice: {libreOfficeStatus}";
    }

    private LibreOfficeConversionEngine CreateLibreOfficeEngine()
    {
        string configuredPath = this._libreOfficePathTextBox.Text.Trim();
        return new LibreOfficeConversionEngine(string.IsNullOrEmpty(configuredPath) ? null : configuredPath);
    }

    private void OnLocationModeChanged(object? sender, EventArgs e)
    {
        bool separateSelected = this._separateFolderRadio.Checked;
        this._separateFolderTextBox.Enabled = separateSelected;
        this._separateFolderBrowseButton.Enabled = separateSelected;
    }

    private void OnBrowseSourceClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            this._folderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OnBrowseSeparateFolderClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            this._separateFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OnBrowseLibreOfficePathClick(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "soffice.exe|soffice.exe|All files (*.*)|*.*",
            Title = "Locate soffice.exe"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            this._libreOfficePathTextBox.Text = dialog.FileName;
            this.RefreshEngineStatusLabel();
        }
    }

    private void OnScanClick(object? sender, EventArgs e)
    {
        string folder = this._folderTextBox.Text.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(
                this,
                "Please choose a source folder that exists.",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        OutputLocationMode locationMode = this._separateFolderRadio.Checked
            ? OutputLocationMode.SeparateFolder
            : OutputLocationMode.SameFolder;

        string? separateOutputRoot = this._separateFolderTextBox.Text.Trim();
        if (locationMode == OutputLocationMode.SeparateFolder && string.IsNullOrEmpty(separateOutputRoot))
        {
            MessageBox.Show(
                this,
                "Please choose a separate output folder, or switch back to \"Same folder as the source\".",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        List<OutputTarget> targets = this.GetSelectedTargets();
        if (targets.Count == 0)
        {
            MessageBox.Show(
                this,
                "Choose at least one output target (PDF and/or ODG).",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        IPubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = folder,
            Recursive = this._recursiveCheckBox.Checked,
            EnabledTargets = targets,
            LocationMode = locationMode,
            SeparateOutputRoot = separateOutputRoot
        };

        IReadOnlyList<ConversionRow> rows;
        try
        {
            rows = scanner.Scan(options);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Scan failed: {ex.Message}",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        this._lastScanRows = rows;
        this.PopulateGrid(rows);
        this.RefreshEngineStatusLabel();
    }

    private void PopulateGrid(IReadOnlyList<ConversionRow> rows)
    {
        this._resultsGrid.Rows.Clear();
        foreach (ConversionRow row in rows)
        {
            string pdfStatus = DescribeStatus(row.GetOutput(OutputTarget.Pdf));
            string odgStatus = DescribeStatus(row.GetOutput(OutputTarget.Odg));

            int rowIndex = this._resultsGrid.Rows.Add(false, row.Source.SourceFolder, row.Source.FileName, pdfStatus, odgStatus);

            // Fully converted files start unchecked so a routine "select all and
            // convert" doesn't silently re-convert (and overwrite) finished work.
            this._resultsGrid.Rows[rowIndex].Cells[SelectColumnName].Value = false;
        }
    }

    private static string DescribeStatus(OutputInfo? output)
    {
        if (output is null)
        {
            return "—";
        }

        return output.Exists ? "Converted" : "Not converted";
    }

    private async void OnConvertSelectedClick(object? sender, EventArgs e)
    {
        IReadOnlyList<OutputTarget> wantedTargets = this.GetSelectedTargets();
        if (wantedTargets.Count == 0)
        {
            MessageBox.Show(
                this,
                "Choose at least one output target (PDF and/or ODG).",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        List<(int RowIndex, ConversionRow Row)> selected = this.GetSelectedRows();
        if (selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Tick one or more rows in the list first.",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        EngineSelection engineSelection = this.GetSelectedEngineSelection();
        PublisherConversionEngine publisherEngine = new PublisherConversionEngine();
        LibreOfficeConversionEngine libreOfficeEngine = this.CreateLibreOfficeEngine();
        IConversionEngine? pubToPdfEngine = new EngineResolver().ResolveEngine(engineSelection, publisherEngine, libreOfficeEngine);

        if (pubToPdfEngine is null)
        {
            MessageBox.Show(
                this,
                this.DescribeEngineUnavailable(engineSelection),
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // ODG is always produced via LibreOffice specifically (CLAUDE.md section 3.2),
        // regardless of which engine is producing the PDF it's generated from.
        RowConverter rowConverter = new RowConverter(pubToPdfEngine, libreOfficeEngine);

        List<(int RowIndex, ConversionRow Row)> conflicts = selected
            .Where(item => wantedTargets.Any(target => item.Row.GetOutput(target) is { Exists: true }))
            .ToList();

        List<(int RowIndex, ConversionRow Row)> toConvert = selected;
        int preSkippedCount = 0;

        if (conflicts.Count > 0)
        {
            OverwriteBehavior behavior = this.GetSelectedOverwriteBehavior();

            if (behavior == OverwriteBehavior.Skip)
            {
                toConvert = selected.Except(conflicts).ToList();
                preSkippedCount = conflicts.Count;
            }
            else if (behavior == OverwriteBehavior.Ask)
            {
                using OverwriteConfirmationDialog dialog = new OverwriteConfirmationDialog(conflicts.Count, selected.Count);
                dialog.ShowDialog(this);

                switch (dialog.Choice)
                {
                    case OverwriteChoice.SkipThese:
                        toConvert = selected.Except(conflicts).ToList();
                        preSkippedCount = conflicts.Count;
                        break;
                    case OverwriteChoice.Cancel:
                        return;
                    case OverwriteChoice.OverwriteAll:
                    default:
                        // toConvert already equals selected.
                        break;
                }
            }
            // OverwriteBehavior.Overwrite: proceed with everything selected, no prompt.
        }

        if (toConvert.Count == 0)
        {
            return;
        }

        this._scanButton.Enabled = false;
        this._convertButton.Enabled = false;
        int successCount = 0;
        int failureCount = 0;

        foreach ((int rowIndex, ConversionRow row) in toConvert)
        {
            if (wantedTargets.Contains(OutputTarget.Pdf))
            {
                this._resultsGrid.Rows[rowIndex].Cells[PdfStatusColumnName].Value = "Converting...";
            }

            if (wantedTargets.Contains(OutputTarget.Odg))
            {
                this._resultsGrid.Rows[rowIndex].Cells[OdgStatusColumnName].Value = "Converting...";
            }

            RowConversionResult result = await Task.Run(() => rowConverter.Convert(row, wantedTargets));

            ConversionOutcome? pdfOutcome = result.ForTarget(OutputTarget.Pdf);
            if (pdfOutcome is not null)
            {
                this._resultsGrid.Rows[rowIndex].Cells[PdfStatusColumnName].Value =
                    pdfOutcome.Success ? "Converted" : $"Failed: {pdfOutcome.ErrorMessage}";
                if (pdfOutcome.Success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            ConversionOutcome? odgOutcome = result.ForTarget(OutputTarget.Odg);
            if (odgOutcome is not null)
            {
                this._resultsGrid.Rows[rowIndex].Cells[OdgStatusColumnName].Value =
                    odgOutcome.Success ? "Converted" : $"Failed: {odgOutcome.ErrorMessage}";
                if (odgOutcome.Success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            this._resultsGrid.Rows[rowIndex].Cells[SelectColumnName].Value = false;
        }

        this._scanButton.Enabled = true;
        this._convertButton.Enabled = true;

        string summary = $"Completed {successCount} conversion(s); {failureCount} failed.";
        if (preSkippedCount > 0)
        {
            summary += $" {preSkippedCount} file(s) skipped (already existed).";
        }

        MessageBox.Show(
            this,
            summary,
            "AfterPub",
            MessageBoxButtons.OK,
            failureCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private List<(int RowIndex, ConversionRow Row)> GetSelectedRows()
    {
        List<(int RowIndex, ConversionRow Row)> selected = new List<(int, ConversionRow)>();

        for (int i = 0; i < this._resultsGrid.Rows.Count && i < this._lastScanRows.Count; i++)
        {
            bool isChecked = this._resultsGrid.Rows[i].Cells[SelectColumnName].Value is true;
            if (isChecked)
            {
                selected.Add((i, this._lastScanRows[i]));
            }
        }

        return selected;
    }

    private string DescribeEngineUnavailable(EngineSelection selection)
    {
        return selection switch
        {
            EngineSelection.Publisher => "Publisher is not installed on this machine.",
            EngineSelection.LibreOffice => "LibreOffice was not found. Set its path above, or install it.",
            _ => "Neither Publisher nor LibreOffice was found. Install one, or set a LibreOffice path above."
        };
    }
}
