using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using AfterPub.Core.Settings;
using System.Windows.Forms.VisualStyles;

namespace AfterPub.App;

/// <summary>
/// Minimal-but-real UI: pick a folder and scan it (the scan always reports PDF, ODG and SLA
/// status), then choose what to convert to, the location, engine and overwrite behavior,
/// tick the rows you want, and convert them. No quality
/// options or record file yet (CLAUDE.md section 14).
/// </summary>
public class MainForm : Form
{
    private const string SelectColumnName = "Select";
    private const string PdfStatusColumnName = "PdfStatus";
    private const string OdgStatusColumnName = "OdgStatus";
    private const string SlaStatusColumnName = "SlaStatus";

    // If one file is still converting after this long, say so in its row: an engine may be
    // waiting on a dialog (for example a missing-font prompt) that needs the person's attention.
    private static readonly TimeSpan SlowConversionNoticeDelay = TimeSpan.FromSeconds(10);
    private const string SlowConversionNotice = "Still working... (an engine may be waiting on a dialog)";

    private readonly AppSettingsStore _settingsStore;
    private List<ConversionRow> _lastScanRows = new List<ConversionRow>();

    // State of the select-all checkbox drawn in the grid's first column header.
    private CheckState _headerCheckState = CheckState.Unchecked;
    private bool _suspendHeaderUpdates;

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
    private readonly CheckBox _slaTargetCheckBox;

    private readonly GroupBox _engineGroup;
    private readonly RadioButton _autoEngineRadio;
    private readonly RadioButton _publisherEngineRadio;
    private readonly RadioButton _libreOfficeEngineRadio;
    private readonly RadioButton _scribusEngineRadio;
    private readonly Label _libreOfficePathLabel;
    private readonly TextBox _libreOfficePathTextBox;
    private readonly Button _libreOfficePathBrowseButton;
    private readonly Label _scribusPathLabel;
    private readonly TextBox _scribusPathTextBox;
    private readonly Button _scribusPathBrowseButton;
    private readonly Label _engineStatusLabel;

    private readonly Button _convertButton;
    private readonly Button _selectNotConvertedButton;
    private readonly Button _cancelButton;
    private readonly Button _abortButton;

    // Shared by the engines that run as separate processes, so Abort now can stop them.
    private readonly ProcessTracker _processTracker = new ProcessTracker();
    private bool _cancelRequested;

    // Non-null only while a scan is running; Cancel cancels the scan instead of a conversion.
    private CancellationTokenSource? _scanCancellation;
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
        this.Height = 800;
        this.MinimumSize = new Size(700, 724);

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
            Text = "Convert to",
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

        this._slaTargetCheckBox = new CheckBox
        {
            Text = "SLA (editable in Scribus — needs Scribus)",
            Left = 530,
            Top = 22,
            Width = 320
        };

        this._targetsGroup.Controls.Add(this._pdfTargetCheckBox);
        this._targetsGroup.Controls.Add(this._odgTargetCheckBox);
        this._targetsGroup.Controls.Add(this._slaTargetCheckBox);

        // --- Row 4: conversion engine ---

        this._engineGroup = new GroupBox
        {
            Text = "PDF engine (ODG always uses LibreOffice, SLA always uses Scribus)",
            Left = 10,
            Top = 208,
            Width = 860,
            Height = 164,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._autoEngineRadio = new RadioButton
        {
            Text = "Auto (Publisher, then LibreOffice, then Scribus)",
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

        this._scribusEngineRadio = new RadioButton
        {
            Text = "Scribus",
            Left = 310,
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

        this._scribusPathLabel = new Label
        {
            Text = "Scribus path (optional):",
            Left = 10,
            Top = 100,
            Width = 180
        };

        this._scribusPathTextBox = new TextBox
        {
            Left = 200,
            Top = 97,
            Width = 550,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._scribusPathBrowseButton = new Button
        {
            Text = "Browse...",
            Left = 760,
            Top = 95,
            Width = 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._scribusPathBrowseButton.Click += this.OnBrowseScribusPathClick;

        // Two lines: Publisher and LibreOffice on the first, Scribus on the second,
        // so long install paths do not run off the edge of the window.
        this._engineStatusLabel = new Label
        {
            Left = 10,
            Top = 126,
            Width = 840,
            Height = 34,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._engineGroup.Controls.Add(this._autoEngineRadio);
        this._engineGroup.Controls.Add(this._publisherEngineRadio);
        this._engineGroup.Controls.Add(this._libreOfficeEngineRadio);
        this._engineGroup.Controls.Add(this._scribusEngineRadio);
        this._engineGroup.Controls.Add(this._libreOfficePathLabel);
        this._engineGroup.Controls.Add(this._libreOfficePathTextBox);
        this._engineGroup.Controls.Add(this._libreOfficePathBrowseButton);
        this._engineGroup.Controls.Add(this._scribusPathLabel);
        this._engineGroup.Controls.Add(this._scribusPathTextBox);
        this._engineGroup.Controls.Add(this._scribusPathBrowseButton);
        this._engineGroup.Controls.Add(this._engineStatusLabel);

        // --- Row 5: convert button ---

        this._convertButton = new Button
        {
            Text = "Convert selected",
            Left = 10,
            Top = 382,
            Width = 160,
            Height = 26
        };
        this._convertButton.Click += this.OnConvertSelectedClick;

        this._selectNotConvertedButton = new Button
        {
            Text = "Select not converted",
            Left = 520,
            Top = 382,
            Width = 170,
            Height = 26
        };
        this._selectNotConvertedButton.Click += this.OnSelectNotConvertedClick;

        this._cancelButton = new Button
        {
            Text = "Cancel",
            Left = 700,
            Top = 382,
            Width = 80,
            Height = 26,
            Enabled = false
        };
        this._cancelButton.Click += this.OnCancelClick;

        this._abortButton = new Button
        {
            Text = "Abort now",
            Left = 790,
            Top = 382,
            Width = 80,
            Height = 26,
            Enabled = false
        };
        this._abortButton.Click += this.OnAbortClick;

        this._overwriteLabel = new Label
        {
            Text = "If output exists:",
            Left = 185,
            Top = 387,
            Width = 100
        };

        this._overwriteAskRadio = new RadioButton
        {
            Text = "Ask",
            Left = 285,
            Top = 385,
            Width = 55,
            Checked = true
        };

        this._overwriteSkipRadio = new RadioButton
        {
            Text = "Skip",
            Left = 345,
            Top = 385,
            Width = 60
        };

        this._overwriteOverwriteRadio = new RadioButton
        {
            Text = "Overwrite",
            Left = 410,
            Top = 385,
            Width = 90
        };

        // --- Results grid ---

        this._resultsGrid = new DataGridView
        {
            Left = 10,
            Top = 418,
            Width = 860,
            Height = 306,
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
        this._resultsGrid.Columns.Add(SlaStatusColumnName, "SLA");

        foreach (DataGridViewColumn column in this._resultsGrid.Columns)
        {
            if (column.Name != SelectColumnName)
            {
                column.ReadOnly = true;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }

        // Header select-all checkbox: the grid has no built-in one, so it is drawn in the
        // first column's header and a click on that header toggles every row.
        this._resultsGrid.CellPainting += this.OnResultsGridCellPainting;
        this._resultsGrid.ColumnHeaderMouseClick += this.OnResultsGridColumnHeaderMouseClick;
        this._resultsGrid.CurrentCellDirtyStateChanged += this.OnResultsGridCurrentCellDirtyStateChanged;
        this._resultsGrid.CellValueChanged += this.OnResultsGridCellValueChanged;

        // The engine radio buttons only matter for the PDF stage (PDF, and the PDF that ODG is
        // made from). SLA does not use a PDF at all, so SLA-only leaves them greyed out.
        this._pdfTargetCheckBox.CheckedChanged += this.OnPdfStageTargetsChanged;
        this._odgTargetCheckBox.CheckedChanged += this.OnPdfStageTargetsChanged;

        this.Controls.Add(this._folderTextBox);
        this.Controls.Add(this._browseButton);
        this.Controls.Add(this._recursiveCheckBox);
        this.Controls.Add(this._scanButton);
        this.Controls.Add(this._outputLocationGroup);
        this.Controls.Add(this._targetsGroup);
        this.Controls.Add(this._engineGroup);
        this.Controls.Add(this._convertButton);
        this.Controls.Add(this._selectNotConvertedButton);
        this.Controls.Add(this._cancelButton);
        this.Controls.Add(this._abortButton);
        this.Controls.Add(this._overwriteLabel);
        this.Controls.Add(this._overwriteAskRadio);
        this.Controls.Add(this._overwriteSkipRadio);
        this.Controls.Add(this._overwriteOverwriteRadio);
        this.Controls.Add(this._resultsGrid);

        this.LoadSettingsIntoControls();
        this.UpdateEngineRadioState();
        this.RefreshEngineStatusLabel();
        this.FormClosing += this.OnFormClosing;
    }

    // F1 opens the About box from anywhere in the window, even while the grid has focus.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1)
        {
            this.ShowAbout();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowAbout()
    {
        using AboutForm about = new AboutForm();
        about.ShowDialog(this);
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
        this._slaTargetCheckBox.Checked = settings.EnabledTargets.Contains(OutputTarget.Sla);

        switch (settings.EngineSelection)
        {
            case EngineSelection.Publisher:
                this._publisherEngineRadio.Checked = true;
                break;
            case EngineSelection.LibreOffice:
                this._libreOfficeEngineRadio.Checked = true;
                break;
            case EngineSelection.Scribus:
                this._scribusEngineRadio.Checked = true;
                break;
            default:
                this._autoEngineRadio.Checked = true;
                break;
        }

        this._libreOfficePathTextBox.Text = settings.LibreOfficePath ?? string.Empty;
        this._scribusPathTextBox.Text = settings.ScribusPath ?? string.Empty;

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
        this._scanCancellation?.Cancel();

        AppSettings settings = new AppSettings
        {
            Recursive = this._recursiveCheckBox.Checked,
            LocationMode = this._separateFolderRadio.Checked
                ? OutputLocationMode.SeparateFolder
                : OutputLocationMode.SameFolder,
            SeparateOutputRoot = this._separateFolderTextBox.Text.Trim(),
            EngineSelection = this.GetSelectedEngineSelection(),
            LibreOfficePath = this._libreOfficePathTextBox.Text.Trim(),
            ScribusPath = this._scribusPathTextBox.Text.Trim(),
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

        if (this._slaTargetCheckBox.Checked)
        {
            targets.Add(OutputTarget.Sla);
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

        if (this._scribusEngineRadio.Checked)
        {
            return EngineSelection.Scribus;
        }

        return EngineSelection.Auto;
    }

    private void RefreshEngineStatusLabel()
    {
        PublisherConversionEngine publisherEngine = new PublisherConversionEngine();
        LibreOfficeConversionEngine libreOfficeEngine = this.CreateLibreOfficeEngine();
        ScribusConversionEngine scribusEngine = this.CreateScribusEngine();

        string publisherStatus = publisherEngine.IsAvailable() ? "found" : "not found";
        string libreOfficeStatus = libreOfficeEngine.ResolvedExecutablePath is string libreOfficePath
            ? $"found at {libreOfficePath}"
            : "not found";
        string scribusStatus = scribusEngine.ResolvedExecutablePath is string scribusPath
            ? $"found at {scribusPath}"
            : "not found";

        // SLA is produced by Scribus, so offer it only when Scribus was found.
        bool scribusFound = scribusEngine.ResolvedExecutablePath is not null;
        this._slaTargetCheckBox.Enabled = scribusFound;
        if (!scribusFound)
        {
            this._slaTargetCheckBox.Checked = false;
        }

        this._engineStatusLabel.Text =
            $"Detected — Publisher: {publisherStatus}    LibreOffice: {libreOfficeStatus}"
            + Environment.NewLine
            + $"Scribus: {scribusStatus}";
    }

    private LibreOfficeConversionEngine CreateLibreOfficeEngine()
    {
        string configuredPath = this._libreOfficePathTextBox.Text.Trim();
        return new LibreOfficeConversionEngine(string.IsNullOrEmpty(configuredPath) ? null : configuredPath, this._processTracker);
    }

    private ScribusConversionEngine CreateScribusEngine()
    {
        string configuredPath = this._scribusPathTextBox.Text.Trim();
        return new ScribusConversionEngine(string.IsNullOrEmpty(configuredPath) ? null : configuredPath, this._processTracker);
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

    private void OnBrowseScribusPathClick(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "Scribus.exe|Scribus.exe|All files (*.*)|*.*",
            Title = "Locate Scribus.exe"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            this._scribusPathTextBox.Text = dialog.FileName;
            this.RefreshEngineStatusLabel();
        }
    }

    private async void OnScanClick(object? sender, EventArgs e)
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

        // The scan always looks for .pub files and reports PDF, ODG and SLA status for each,
        // whatever is ticked under "Convert to". Those checkboxes only affect conversion.
        IPubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = folder,
            Recursive = this._recursiveCheckBox.Checked,
            LocationMode = locationMode,
            SeparateOutputRoot = separateOutputRoot
        };

        // While scanning, the Cancel button cancels the scan. Conversion controls stay off.
        string originalTitle = this.Text;
        this._scanButton.Enabled = false;
        this._convertButton.Enabled = false;
        this._selectNotConvertedButton.Enabled = false;
        this._cancelButton.Enabled = true;
        this._cancelButton.Text = "Cancel";

        CancellationTokenSource cancellation = new CancellationTokenSource();
        this._scanCancellation = cancellation;

        // Progress<T> created on the UI thread posts its callback back to the UI thread.
        Progress<int> progress = new Progress<int>(count => this.Text = $"{originalTitle} - scanning, {count} found");

        IReadOnlyList<ConversionRow>? rows = null;
        string? errorMessage = null;
        bool wasCancelled = false;

        try
        {
            rows = await Task.Run(() => scanner.Scan(options, cancellation.Token, progress), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            this._scanCancellation = null;
            cancellation.Dispose();
        }

        if (this.IsDisposed)
        {
            return;
        }

        this.Text = originalTitle;
        this._scanButton.Enabled = true;
        this._convertButton.Enabled = true;
        this._selectNotConvertedButton.Enabled = true;
        this._cancelButton.Enabled = false;
        this._cancelButton.Text = "Cancel";

        if (wasCancelled)
        {
            // Keep whatever the grid showed before; a half-finished scan is not shown.
            return;
        }

        if (errorMessage is not null || rows is null)
        {
            MessageBox.Show(
                this,
                $"Scan failed: {errorMessage}",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        this._lastScanRows = rows.ToList();
        this.PopulateGrid(rows);
        this.RefreshEngineStatusLabel();

        if (rows.Count == 0)
        {
            MessageBox.Show(
                this,
                "No .pub files found.",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void PopulateGrid(IReadOnlyList<ConversionRow> rows)
    {
        this._suspendHeaderUpdates = true;
        this._resultsGrid.Rows.Clear();
        foreach (ConversionRow row in rows)
        {
            string pdfStatus = DescribeStatus(row.GetOutput(OutputTarget.Pdf));
            string odgStatus = DescribeStatus(row.GetOutput(OutputTarget.Odg));
            string slaStatus = DescribeStatus(row.GetOutput(OutputTarget.Sla));

            int rowIndex = this._resultsGrid.Rows.Add(false, row.Source.SourceFolder, row.Source.FileName, pdfStatus, odgStatus, slaStatus);

            // Fully converted files start unchecked so a routine "select all and
            // convert" doesn't silently re-convert (and overwrite) finished work.
            this._resultsGrid.Rows[rowIndex].Cells[SelectColumnName].Value = false;
        }

        this._suspendHeaderUpdates = false;
        this.UpdateHeaderCheckState();
    }

    private void OnPdfStageTargetsChanged(object? sender, EventArgs e)
    {
        this.UpdateEngineRadioState();
    }

    private void UpdateEngineRadioState()
    {
        bool needsPdfStage = this._pdfTargetCheckBox.Checked || this._odgTargetCheckBox.Checked;

        this._autoEngineRadio.Enabled = needsPdfStage;
        this._publisherEngineRadio.Enabled = needsPdfStage;
        this._libreOfficeEngineRadio.Enabled = needsPdfStage;
        this._scribusEngineRadio.Enabled = needsPdfStage;
    }

    // Updates the status cells of the targets currently being converted for one row.
    private void SetPendingStatus(int rowIndex, IReadOnlyList<OutputTarget> targets, string text)
    {
        DataGridViewRow gridRow = this._resultsGrid.Rows[rowIndex];

        if (targets.Contains(OutputTarget.Pdf))
        {
            gridRow.Cells[PdfStatusColumnName].Value = text;
        }

        if (targets.Contains(OutputTarget.Odg))
        {
            gridRow.Cells[OdgStatusColumnName].Value = text;
        }

        if (targets.Contains(OutputTarget.Sla))
        {
            gridRow.Cells[SlaStatusColumnName].Value = text;
        }
    }

    // --- Cancel and Abort (CLAUDE.md section 3.4) ---

    private void OnCancelClick(object? sender, EventArgs e)
    {
        // A scan in progress is cancelled first; there is no conversion running at the same time.
        if (this._scanCancellation is not null)
        {
            this._scanCancellation.Cancel();
            this._cancelButton.Enabled = false;
            this._cancelButton.Text = "Cancelling...";
            return;
        }

        // Finish the file currently converting, then stop. Files not yet started stay
        // untouched and still show as not converted.
        this._cancelRequested = true;
        this._cancelButton.Enabled = false;
        this._cancelButton.Text = "Cancelling...";
    }

    private void OnAbortClick(object? sender, EventArgs e)
    {
        // Stops the engine process immediately: LibreOffice and Scribus run as processes the
        // app started, and for Publisher the engine registers only the Publisher process it
        // started itself, never one the person already had open.
        this._cancelRequested = true;
        this._cancelButton.Enabled = false;
        this._abortButton.Enabled = false;
        this._processTracker.AbortAll();
    }

    // --- Selection helpers (select-all header checkbox and "Select not converted") ---

    private void OnSelectNotConvertedClick(object? sender, EventArgs e)
    {
        // "Not converted" follows CLAUDE.md section 3.3: a row still needs work when any
        // target currently ticked in "Output targets" has no output file yet. A target the
        // last scan did not include counts as missing too.
        List<OutputTarget> wantedTargets = this.GetSelectedTargets();

        this._resultsGrid.EndEdit();
        this._suspendHeaderUpdates = true;

        for (int i = 0; i < this._resultsGrid.Rows.Count && i < this._lastScanRows.Count; i++)
        {
            ConversionRow row = this._lastScanRows[i];
            bool needsWork = wantedTargets.Any(target => row.GetOutput(target) is not { Exists: true });
            this._resultsGrid.Rows[i].Cells[SelectColumnName].Value = needsWork;
        }

        this._suspendHeaderUpdates = false;
        this.UpdateHeaderCheckState();
    }

    private void OnResultsGridColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.ColumnIndex != this._resultsGrid.Columns[SelectColumnName]!.Index)
        {
            return;
        }

        // Everything ticked -> clear all; anything else (none or some) -> tick all.
        this.SetAllRowsChecked(this._headerCheckState != CheckState.Checked);
    }

    private void SetAllRowsChecked(bool isChecked)
    {
        this._resultsGrid.EndEdit();
        this._suspendHeaderUpdates = true;

        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            gridRow.Cells[SelectColumnName].Value = isChecked;
        }

        this._suspendHeaderUpdates = false;
        this.UpdateHeaderCheckState();
    }

    // A checkbox cell only commits its value when it loses focus; commit immediately so
    // the header checkbox follows each click.
    private void OnResultsGridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (this._resultsGrid.IsCurrentCellDirty)
        {
            this._resultsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnResultsGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex == this._resultsGrid.Columns[SelectColumnName]!.Index)
        {
            this.UpdateHeaderCheckState();
        }
    }

    private void UpdateHeaderCheckState()
    {
        if (this._suspendHeaderUpdates)
        {
            return;
        }

        int total = this._resultsGrid.Rows.Count;
        int ticked = 0;
        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            if (gridRow.Cells[SelectColumnName].Value is true)
            {
                ticked++;
            }
        }

        CheckState newState = ticked == 0
            ? CheckState.Unchecked
            : (ticked == total ? CheckState.Checked : CheckState.Indeterminate);

        if (newState != this._headerCheckState)
        {
            this._headerCheckState = newState;
            this._resultsGrid.InvalidateCell(this._resultsGrid.Columns[SelectColumnName]!.Index, -1);
        }
    }

    private void OnResultsGridCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.Graphics is null || e.ColumnIndex != this._resultsGrid.Columns[SelectColumnName]!.Index)
        {
            return;
        }

        // Draw the normal header (background, borders), then the checkbox on top of it.
        e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

        CheckBoxState glyphState = this._headerCheckState switch
        {
            CheckState.Checked => CheckBoxState.CheckedNormal,
            CheckState.Indeterminate => CheckBoxState.MixedNormal,
            _ => CheckBoxState.UncheckedNormal
        };

        Size glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, glyphState);
        Point glyphLocation = new Point(
            e.CellBounds.X + ((e.CellBounds.Width - glyphSize.Width) / 2),
            e.CellBounds.Y + ((e.CellBounds.Height - glyphSize.Height) / 2));
        CheckBoxRenderer.DrawCheckBox(e.Graphics, glyphLocation, glyphState);

        e.Handled = true;
    }

    // Rebuilds a row with its output files re-checked on disk. The source is unchanged.
    private static ConversionRow RefreshRow(ConversionRow row)
    {
        List<OutputInfo> refreshed = new List<OutputInfo>();
        foreach (OutputInfo output in row.Outputs)
        {
            refreshed.Add(OutputInfo.FromFileSystem(output.Target, output.ExpectedPath));
        }

        return new ConversionRow(row.Source, refreshed);
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
                "Choose at least one output target (PDF, ODG and/or SLA).",
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
        PublisherConversionEngine publisherEngine = new PublisherConversionEngine(this._processTracker);
        LibreOfficeConversionEngine libreOfficeEngine = this.CreateLibreOfficeEngine();
        ScribusConversionEngine scribusEngine = this.CreateScribusEngine();
        IConversionEngine? pubToPdfEngine = new EngineResolver().ResolveEngine(
            engineSelection,
            publisherEngine,
            libreOfficeEngine,
            scribusEngine);

        // The PDF engine is only needed when PDF or ODG is wanted. An SLA-only run goes
        // straight to Scribus and must not fail because the chosen PDF engine is missing.
        bool needsPdfStage = wantedTargets.Contains(OutputTarget.Pdf) || wantedTargets.Contains(OutputTarget.Odg);

        if (needsPdfStage && pubToPdfEngine is null)
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
        // SLA is always produced by Scribus specifically, saved straight from the .pub,
        // regardless of which engine produces the PDF.
        RowConverter rowConverter = new RowConverter(pubToPdfEngine, libreOfficeEngine, scribusEngine);

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

        // Publisher is the only engine that preserves everything; LibreOffice and Scribus read
        // .pub through libmspub, which can silently drop text, so say which engine was used.
        EngineKind? pdfEngineKind = needsPdfStage ? pubToPdfEngine?.Kind : null;
        bool usedOpenSourceEngine = pdfEngineKind is not null && pdfEngineKind != EngineKind.Publisher;
        string pdfSuccessText = usedOpenSourceEngine ? $"Converted ({pdfEngineKind})" : "Converted";

        this._scanButton.Enabled = false;
        this._convertButton.Enabled = false;

        // Cancel finishes the file being converted, then stops. Abort now also stops the
        // engine process immediately (CLAUDE.md section 3.4).
        this._cancelRequested = false;
        this._processTracker.Reset();
        this._cancelButton.Enabled = true;
        this._abortButton.Enabled = true;

        int successCount = 0;
        int failureCount = 0;
        int processedCount = 0;

        foreach ((int rowIndex, ConversionRow row) in toConvert)
        {
            if (this._cancelRequested)
            {
                break;
            }

            if (wantedTargets.Contains(OutputTarget.Pdf))
            {
                this._resultsGrid.Rows[rowIndex].Cells[PdfStatusColumnName].Value = "Converting...";
            }

            if (wantedTargets.Contains(OutputTarget.Odg))
            {
                this._resultsGrid.Rows[rowIndex].Cells[OdgStatusColumnName].Value = "Converting...";
            }

            if (wantedTargets.Contains(OutputTarget.Sla))
            {
                this._resultsGrid.Rows[rowIndex].Cells[SlaStatusColumnName].Value = "Converting...";
            }

            Task<RowConversionResult> conversionTask = Task.Run(() => rowConverter.Convert(row, wantedTargets));
            Task firstToFinish = await Task.WhenAny(conversionTask, Task.Delay(SlowConversionNoticeDelay));
            if (firstToFinish != conversionTask)
            {
                this.SetPendingStatus(rowIndex, wantedTargets, SlowConversionNotice);
            }

            RowConversionResult result = await conversionTask;

            // The scan data is a snapshot, so re-read this row's outputs from disk now that the
            // engines have run. "Select not converted" and the overwrite check then see the
            // current state instead of the state at scan time.
            this._lastScanRows[rowIndex] = RefreshRow(row);

            ConversionOutcome? pdfOutcome = result.ForTarget(OutputTarget.Pdf);
            if (pdfOutcome is not null)
            {
                this._resultsGrid.Rows[rowIndex].Cells[PdfStatusColumnName].Value =
                    pdfOutcome.Success ? pdfSuccessText : $"Failed: {pdfOutcome.ErrorMessage}";
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

            ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
            if (slaOutcome is not null)
            {
                this._resultsGrid.Rows[rowIndex].Cells[SlaStatusColumnName].Value =
                    slaOutcome.Success ? "Converted (Scribus)" : $"Failed: {slaOutcome.ErrorMessage}";
                if (slaOutcome.Success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            this._resultsGrid.Rows[rowIndex].Cells[SelectColumnName].Value = false;
            processedCount++;
        }

        this._scanButton.Enabled = true;
        this._convertButton.Enabled = true;
        this._cancelButton.Enabled = false;
        this._cancelButton.Text = "Cancel";
        this._abortButton.Enabled = false;

        string summary = $"Completed {successCount} conversion(s); {failureCount} failed.";
        if (preSkippedCount > 0)
        {
            summary += $" {preSkippedCount} file(s) skipped (already existed).";
        }

        if (this._cancelRequested)
        {
            int notProcessed = toConvert.Count - processedCount;
            summary += this._processTracker.AbortRequested ? " Aborted by the user." : " Cancelled by the user.";
            if (notProcessed > 0)
            {
                summary += $" {notProcessed} file(s) were not converted.";
            }
        }

        List<string> lossyEngines = new List<string>();
        if (usedOpenSourceEngine && pdfEngineKind is { } openSourceEngineKind)
        {
            lossyEngines.Add(openSourceEngineKind.ToString());
        }

        if (wantedTargets.Contains(OutputTarget.Sla) && !lossyEngines.Contains("Scribus"))
        {
            lossyEngines.Add("Scribus");
        }

        if (lossyEngines.Count > 0 && successCount > 0)
        {
            summary += $" {string.Join(" and ", lossyEngines)} can silently drop text from .pub files, so check the output against the originals.";
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
            EngineSelection.Scribus => "Scribus was not found. Set its path above, or install it.",
            _ => "None of Publisher, LibreOffice or Scribus was found. Install one, or set a LibreOffice or Scribus path above."
        };
    }
}
