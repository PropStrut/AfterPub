using System.ComponentModel;
using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using AfterPub.Core.Settings;
using System.Windows.Forms.VisualStyles;

namespace AfterPub.App;

/// <summary>
/// Minimal-but-real UI: pick a folder and scan it (the scan always reports PDF, ODG and SLA
/// status), then choose what to convert to, the location, engine and overwrite behavior,
/// tick the rows you want, and convert them. The grid can be sorted by any column and
/// filtered by name or by which outputs are missing. No quality options or record file yet
/// (CLAUDE.md section 14).
/// </summary>
public class MainForm : Form
{
    private const string AppTitle = "AfterPub";
    private const string SelectColumnName = "Select";
    private const string FolderColumnName = "SourceFolder";
    private const string FileNameColumnName = "FileName";
    private const string SourceInfoColumnName = "SourceInfo";
    private const string PdfStatusColumnName = "PdfStatus";
    private const string OdgStatusColumnName = "OdgStatus";
    private const string SlaStatusColumnName = "SlaStatus";

    // If one file is still converting after this long, say so in its row: an engine may be
    // waiting on a dialog (for example a missing-font prompt) that needs the person's attention.
    private static readonly TimeSpan SlowConversionNoticeDelay = TimeSpan.FromSeconds(10);
    private const string SlowConversionNotice = "Still working... (an engine may be waiting on a dialog)";

    private readonly AppSettingsStore _settingsStore;

    // The color mode saved in the settings file. The running window was created in the mode
    // that was saved at startup; a change here takes effect on the next start (F2).
    private AppColorMode _colorMode;

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

    // The settings area (output location, convert-to formats, engine, overwrite) is collapsed
    // by default; the toggle button above it also summarizes the current choices.
    private readonly Button _settingsToggleButton;
    private readonly Panel _settingsPanel;
    private bool _settingsExpanded;
    private readonly Label _filterModeLabel;
    private readonly ComboBox _filterModeComboBox;
    private readonly Label _filterTextLabel;
    private readonly TextBox _filterTextBox;
    private readonly Label _filterCountLabel;

    public MainForm()
    {
        this._settingsStore = new AppSettingsStore(AppSettingsStore.GetDefaultFilePath());
        this._colorMode = this._settingsStore.Load().ColorMode;

        this.Text = "AfterPub";
        this.Width = 900;
        this.Height = 640;
        this.MinimumSize = new Size(700, 480);

        // --- Scan row (top of the window, the app's main action): folder, recursive, scan ---

        Font scanRowFont = new Font(this.Font.FontFamily, 11f);

        this._folderTextBox = new TextBox
        {
            Left = 10,
            Top = 12,
            Width = 450,
            Font = scanRowFont,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        this._browseButton = new Button
        {
            Text = "Browse...",
            Left = 470,
            Top = 11,
            Width = 90,
            Height = 31,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._browseButton.Click += this.OnBrowseSourceClick;

        this._recursiveCheckBox = new CheckBox
        {
            Text = "Include subfolders",
            Left = 570,
            Top = 17,
            Width = 150,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        this._scanButton = new Button
        {
            Text = "Scan",
            Left = 730,
            Top = 9,
            Width = 140,
            Height = 36,
            Font = new Font(this.Font.FontFamily, 11f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        this._scanButton.Click += this.OnScanClick;

        // --- Row 2: output location ---

        this._outputLocationGroup = new GroupBox
        {
            Text = "Output location",
            Left = 10,
            Top = 0,
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
            Top = 100,
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
            Top = 164,
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
            Left = 180,
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
            Left = 10,
            Top = 343,
            Width = 100
        };

        this._overwriteAskRadio = new RadioButton
        {
            Text = "Ask",
            Left = 110,
            Top = 341,
            Width = 55,
            Checked = true
        };

        this._overwriteSkipRadio = new RadioButton
        {
            Text = "Skip",
            Left = 170,
            Top = 341,
            Width = 60
        };

        this._overwriteOverwriteRadio = new RadioButton
        {
            Text = "Overwrite",
            Left = 235,
            Top = 341,
            Width = 90
        };

        // --- Collapsible settings area ---

        this._settingsToggleButton = new Button
        {
            Left = 10,
            Top = 88,
            Width = 860,
            Height = 26,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        this._settingsToggleButton.Click += this.OnSettingsToggleClick;

        this._settingsPanel = new Panel
        {
            Left = 10,
            Top = 118,
            Width = 860,
            Height = 372,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        this._settingsPanel.Controls.Add(this._outputLocationGroup);
        this._settingsPanel.Controls.Add(this._targetsGroup);
        this._settingsPanel.Controls.Add(this._engineGroup);
        this._settingsPanel.Controls.Add(this._overwriteLabel);
        this._settingsPanel.Controls.Add(this._overwriteAskRadio);
        this._settingsPanel.Controls.Add(this._overwriteSkipRadio);
        this._settingsPanel.Controls.Add(this._overwriteOverwriteRadio);

        this._cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this._abortButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // --- Filter row (directly under the scan row) ---

        this._filterModeLabel = new Label
        {
            Text = "Show:",
            Left = 10,
            Top = 60,
            Width = 40
        };

        this._filterModeComboBox = new ComboBox
        {
            Left = 52,
            Top = 56,
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        this._filterModeComboBox.Items.AddRange(new object[]
        {
            "All files",
            "Missing PDF",
            "Missing ODG",
            "Missing SLA",
            "Missing any of PDF / ODG / SLA",
            "Out of date (output older than the .pub)"
        });
        this._filterModeComboBox.SelectedIndex = 0;
        this._filterModeComboBox.SelectedIndexChanged += this.OnFilterChanged;

        this._filterTextLabel = new Label
        {
            Text = "Path contains:",
            Left = 260,
            Top = 60,
            Width = 85
        };

        this._filterTextBox = new TextBox
        {
            Left = 347,
            Top = 56,
            Width = 200
        };
        this._filterTextBox.TextChanged += this.OnFilterChanged;

        this._filterCountLabel = new Label
        {
            Left = 565,
            Top = 60,
            Width = 305,
            AutoEllipsis = true
        };

        // --- Results grid ---

        this._resultsGrid = new DataGridView
        {
            Left = 10,
            Top = 150,
            Width = 860,
            Height = 400,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        DataGridViewCheckBoxColumn selectColumn = new DataGridViewCheckBoxColumn
        {
            Name = SelectColumnName,
            HeaderText = string.Empty,
            FillWeight = 20,
            MinimumWidth = 30,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        this._resultsGrid.Columns.Add(selectColumn);
        this._resultsGrid.Columns.Add(FolderColumnName, "Folder");
        this._resultsGrid.Columns.Add(FileNameColumnName, "File Name");
        this._resultsGrid.Columns.Add(SourceInfoColumnName, ".pub");
        this._resultsGrid.Columns.Add(PdfStatusColumnName, "PDF");
        this._resultsGrid.Columns.Add(OdgStatusColumnName, "ODG");
        this._resultsGrid.Columns.Add(SlaStatusColumnName, "SLA");

        // Every column except the tick box can be sorted by clicking its header. The .pub column
        // and the three output columns show date and size, and sort by date (OnResultsGridSortCompare).
        foreach (DataGridViewColumn column in this._resultsGrid.Columns)
        {
            if (column.Name != SelectColumnName)
            {
                column.ReadOnly = true;
                column.SortMode = DataGridViewColumnSortMode.Automatic;
            }
        }

        this._resultsGrid.Columns[FolderColumnName]!.FillWeight = 90;
        this._resultsGrid.Columns[FileNameColumnName]!.FillWeight = 90;
        this._resultsGrid.Columns[SourceInfoColumnName]!.FillWeight = 70;
        this._resultsGrid.Columns[PdfStatusColumnName]!.FillWeight = 70;
        this._resultsGrid.Columns[OdgStatusColumnName]!.FillWeight = 70;
        this._resultsGrid.Columns[SlaStatusColumnName]!.FillWeight = 70;

        this._resultsGrid.SortCompare += this.OnResultsGridSortCompare;
        this._resultsGrid.CellMouseUp += this.OnResultsGridCellMouseUp;

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
        this.Controls.Add(this._filterModeLabel);
        this.Controls.Add(this._filterModeComboBox);
        this.Controls.Add(this._filterTextLabel);
        this.Controls.Add(this._filterTextBox);
        this.Controls.Add(this._filterCountLabel);
        this.Controls.Add(this._settingsToggleButton);
        this.Controls.Add(this._settingsPanel);
        this.Controls.Add(this._convertButton);
        this.Controls.Add(this._selectNotConvertedButton);
        this.Controls.Add(this._cancelButton);
        this.Controls.Add(this._abortButton);
        this.Controls.Add(this._resultsGrid);

        // Keep the collapsed settings summary in step with the choices it describes.
        this._pdfTargetCheckBox.CheckedChanged += this.OnSettingsSummaryChanged;
        this._odgTargetCheckBox.CheckedChanged += this.OnSettingsSummaryChanged;
        this._slaTargetCheckBox.CheckedChanged += this.OnSettingsSummaryChanged;
        this._autoEngineRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._publisherEngineRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._libreOfficeEngineRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._scribusEngineRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._overwriteAskRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._overwriteSkipRadio.CheckedChanged += this.OnSettingsSummaryChanged;
        this._overwriteOverwriteRadio.CheckedChanged += this.OnSettingsSummaryChanged;

        this.LoadSettingsIntoControls();
        this.ApplyGridColorMode();
        this.UpdateTitle();
        this.UpdateSettingsToggleText();
        this.LayoutMain();
        this.Resize += (sender, args) => this.LayoutMain();
        this.UpdateEngineRadioState();
        this.RefreshEngineStatusLabel();
        this.FormClosing += this.OnFormClosing;
    }

    // F1 opens the About box and F2 switches light/dark, from anywhere in the window,
    // even while the grid has focus. (Both shortcuts are temporary homes until a menu exists.)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1)
        {
            this.ShowAbout();
            return true;
        }

        if (keyData == Keys.F2)
        {
            this.ToggleColorMode();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // The title bar doubles as a low-space reminder of the two shortcut keys. Windows draws the title
    // left-aligned, so the hint follows the name after a gap. The F2 hint names the mode that
    // pressing F2 switches to. While a scan runs, the title shows scan progress instead.
    private string BuildTitle()
    {
        string nextMode = this._colorMode == AppColorMode.Dark ? "Light mode" : "Dark mode";
        return $"{AppTitle}{new string(' ', 12)}F1 About  \u00B7  F2 {nextMode}";
    }

    private void UpdateTitle()
    {
        this.Text = this.BuildTitle();
    }

    // --- Layout: scan row, filter row, settings toggle (and panel when open), buttons, grid ---

    // The controls under the filter row are stacked by hand so the settings area can open and
    // close, and the grid always takes whatever height is left.
    private void LayoutMain()
    {
        int y = 88;

        this._settingsToggleButton.Top = y;
        y += this._settingsToggleButton.Height + 4;

        if (this._settingsExpanded)
        {
            this._settingsPanel.Top = y;
            y += this._settingsPanel.Height + 6;
        }

        this._convertButton.Top = y;
        this._selectNotConvertedButton.Top = y;
        this._cancelButton.Top = y;
        this._abortButton.Top = y;
        y += this._convertButton.Height + 8;

        this._resultsGrid.Top = y;
        this._resultsGrid.Height = Math.Max(100, this.ClientSize.Height - y - 10);
    }

    // How much taller the window actually became when the settings area opened, so closing it
    // can give back exactly that much (it may be less than the area's height on a short screen).
    private int _windowGrowthFromSettings;

    private void OnSettingsToggleClick(object? sender, EventArgs e)
    {
        bool expanding = !this._settingsExpanded;
        int delta = this._settingsPanel.Height + 6;
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int heightBefore = this.Height;

        this._settingsExpanded = expanding;
        this._settingsPanel.Visible = expanding;
        this.UpdateSettingsToggleText();

        // The smallest allowed window height includes the settings area while it is open, but is
        // never more than the usable screen height.
        this.MinimumSize = new Size(700, Math.Min(expanding ? 480 + delta : 480, area.Height));

        if (this.WindowState == FormWindowState.Normal)
        {
            if (expanding)
            {
                // Grow by the size of the settings area so the grid keeps its height, but never
                // taller than the screen can show. On a tall window the grid gives up the difference.
                this.Height = Math.Min(heightBefore + delta, area.Height);
                this._windowGrowthFromSettings = Math.Max(0, this.Height - heightBefore);

                // Growing downward must not push the bottom edge off the screen: slide the window up.
                if (this.Bottom > area.Bottom)
                {
                    this.Top = Math.Max(area.Top, area.Bottom - this.Height);
                }
            }
            else
            {
                this.Height = Math.Max(this.MinimumSize.Height, this.Height - this._windowGrowthFromSettings);
                this._windowGrowthFromSettings = 0;
            }
        }

        this.LayoutMain();
    }

    private void OnSettingsSummaryChanged(object? sender, EventArgs e)
    {
        this.UpdateSettingsToggleText();
    }

    // The button says what is inside, and while the area is closed it also shows the choices that
    // matter most (what gets converted, with which engine, and what happens to existing files),
    // so a hidden setting is never a surprise.
    private void UpdateSettingsToggleText()
    {
        string arrow = this._settingsExpanded ? "\u25BE" : "\u25B8";
        List<OutputTarget> targets = this.GetSelectedTargets();
        string targetText = targets.Count == 0 ? "nothing" : string.Join(" + ", targets);

        this._settingsToggleButton.Text =
            $"{arrow}  Conversion Settings   |   Convert to: {targetText}   |   Engine: {this.GetSelectedEngineSelection()}   |   If output exists: {this.GetSelectedOverwriteBehavior()}";
    }

    // WinForms applies the color mode when windows are created, and switching it in a running
    // app leaves some controls in the old colors, so the choice is saved and applied on restart.
    private void ToggleColorMode()
    {
        if (!this._scanButton.Enabled)
        {
            MessageBox.Show(
                this,
                "Wait for the current scan or conversion to finish before switching light/dark mode.",
                "AfterPub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        this._colorMode = this._colorMode == AppColorMode.Dark ? AppColorMode.Light : AppColorMode.Dark;
        this._settingsStore.Save(this.BuildSettingsFromControls());
        this.UpdateTitle();

        string modeName = this._colorMode == AppColorMode.Dark ? "dark" : "light";
        DialogResult answer = MessageBox.Show(
            this,
            $"AfterPub will use {modeName} mode from the next start. Restart now?",
            "AfterPub",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer == DialogResult.Yes)
        {
            Application.Restart();
        }
    }

    // A grid's header and lines keep their light Windows styling unless they are set explicitly,
    // so in dark mode they are given dark colors here. Light mode leaves the defaults alone.
    private void ApplyGridColorMode()
    {
        if (Application.ColorMode != SystemColorMode.Dark)
        {
            return;
        }

        Color background = Color.FromArgb(32, 32, 32);
        Color headerBackground = Color.FromArgb(48, 48, 48);
        Color text = Color.Gainsboro;

        this._resultsGrid.BackgroundColor = background;
        this._resultsGrid.GridColor = Color.FromArgb(70, 70, 70);
        this._resultsGrid.EnableHeadersVisualStyles = false;

        this._resultsGrid.ColumnHeadersDefaultCellStyle.BackColor = headerBackground;
        this._resultsGrid.ColumnHeadersDefaultCellStyle.ForeColor = text;
        this._resultsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBackground;
        this._resultsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = text;

        this._resultsGrid.DefaultCellStyle.BackColor = background;
        this._resultsGrid.DefaultCellStyle.ForeColor = text;
        this._resultsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 90, 158);
        this._resultsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
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

        // The overwrite choice (Ask / Skip / Overwrite) is deliberately not saved: every start
        // begins with Ask, so a replace-everything choice never carries over to a later session.
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        this._scanCancellation?.Cancel();
        this._settingsStore.Save(this.BuildSettingsFromControls());
    }

    // Everything the settings file holds, taken from the current state of the window. The color
    // mode is part of it, so any save keeps the person's light/dark choice.
    private AppSettings BuildSettingsFromControls()
    {
        return new AppSettings
        {
            Recursive = this._recursiveCheckBox.Checked,
            LocationMode = this._separateFolderRadio.Checked
                ? OutputLocationMode.SeparateFolder
                : OutputLocationMode.SameFolder,
            SeparateOutputRoot = this._separateFolderTextBox.Text.Trim(),
            EngineSelection = this.GetSelectedEngineSelection(),
            LibreOfficePath = this._libreOfficePathTextBox.Text.Trim(),
            ScribusPath = this._scribusPathTextBox.Text.Trim(),
            EnabledTargets = this.GetSelectedTargets().ToList(),
            ColorMode = this._colorMode
        };
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
        this._scanButton.Enabled = false;
        this._convertButton.Enabled = false;
        this._selectNotConvertedButton.Enabled = false;
        this._cancelButton.Enabled = true;
        this._cancelButton.Text = "Cancel";

        CancellationTokenSource cancellation = new CancellationTokenSource();
        this._scanCancellation = cancellation;

        // Progress<T> created on the UI thread posts its callback back to the UI thread.
        Progress<int> progress = new Progress<int>(count => this.Text = $"{AppTitle}  -  scanning, {count} found");

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

        this.UpdateTitle();
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
            // Rows start unticked, so a routine "select all and convert" doesn't silently
            // re-convert (and overwrite) finished work.
            int rowIndex = this._resultsGrid.Rows.Add(
                false,
                row.Source.SourceFolder,
                row.Source.FileName,
                FormatDateAndSize(row.Source.LastWriteTimeUtc, row.Source.SizeBytes),
                string.Empty,
                string.Empty,
                string.Empty);

            // The grid row carries its ConversionRow, so sorting and filtering can reorder or
            // hide rows without any index bookkeeping.
            DataGridViewRow gridRow = this._resultsGrid.Rows[rowIndex];
            gridRow.Tag = row;
            gridRow.Cells[SourceInfoColumnName].ToolTipText = row.Source.FullPath;
            SetStatusCell(gridRow.Cells[PdfStatusColumnName], row, OutputTarget.Pdf, null);
            SetStatusCell(gridRow.Cells[OdgStatusColumnName], row, OutputTarget.Odg, null);
            SetStatusCell(gridRow.Cells[SlaStatusColumnName], row, OutputTarget.Sla, null);
        }

        this._suspendHeaderUpdates = false;

        // Keep the person's chosen sort across a re-scan. Before any choice has been made, sort by
        // folder (then file name) so the order never depends on the file system's own order, and
        // so the header shows its sort arrow.
        if (this._resultsGrid.SortedColumn is { } sortedColumn && this._resultsGrid.SortOrder != SortOrder.None)
        {
            this._resultsGrid.Sort(
                sortedColumn,
                this._resultsGrid.SortOrder == SortOrder.Descending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending);
        }
        else
        {
            this._resultsGrid.Sort(this._resultsGrid.Columns[FolderColumnName]!, ListSortDirection.Ascending);
        }

        this.ApplyFilter();
    }

    // --- Right-click on a PDF, ODG or SLA cell: open the file, or show it in its folder ---
    // Deliberately not offered on the .pub column: the app never opens source documents, so a
    // file the person is only meant to look at cannot be changed through it.

    private ContextMenuStrip? _cellMenu;

    // The menu is shown by hand on a right-click. (The grid's own just-in-time menu event only
    // fires for data-bound or virtual grids, and this one is neither.)
    private void OnResultsGridCellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        OutputTarget? target = this._resultsGrid.Columns[e.ColumnIndex].Name switch
        {
            PdfStatusColumnName => OutputTarget.Pdf,
            OdgStatusColumnName => OutputTarget.Odg,
            SlaStatusColumnName => OutputTarget.Sla,
            _ => null
        };

        if (target is null
            || this._resultsGrid.Rows[e.RowIndex].Tag is not ConversionRow row
            || row.GetOutput(target.Value) is not { Exists: true } output)
        {
            return;
        }

        string path = output.ExpectedPath;
        string? appPath = target.Value switch
        {
            OutputTarget.Odg => this.CreateLibreOfficeEngine().ResolvedExecutablePath,
            OutputTarget.Sla => this.CreateScribusEngine().ResolvedExecutablePath,
            _ => null
        };

        string openText = target.Value switch
        {
            OutputTarget.Odg when appPath is not null => "Open in LibreOffice",
            OutputTarget.Sla when appPath is not null => "Open in Scribus",
            _ => "Open"
        };

        this._cellMenu?.Dispose();
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(openText, null, (object? s, EventArgs args) => this.OpenOutputFile(path, appPath));
        menu.Items.Add("Show in folder", null, (object? s, EventArgs args) => this.ShowInFolder(path));

        this._cellMenu = menu;
        menu.Show(this._resultsGrid, this._resultsGrid.PointToClient(Cursor.Position));
    }

    // Opens with the detected app when there is one (LibreOffice for ODG, Scribus for SLA), and
    // with the Windows default app otherwise.
    private void OpenOutputFile(string path, string? appPath)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, $"This file no longer exists:{Environment.NewLine}{path}", "AfterPub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            System.Diagnostics.ProcessStartInfo startInfo;
            if (appPath is not null)
            {
                startInfo = new System.Diagnostics.ProcessStartInfo(appPath) { UseShellExecute = false };
                startInfo.ArgumentList.Add(path);
            }
            else
            {
                startInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
            }

            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the file: {ex.Message}", "AfterPub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowInFolder(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, $"This file no longer exists:{Environment.NewLine}{path}", "AfterPub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Explorer's own command line: open the folder with this file selected.
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the folder: {ex.Message}", "AfterPub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // --- Sorting and filtering ---

    private void OnResultsGridSortCompare(object? sender, DataGridViewSortCompareEventArgs e)
    {
        ConversionRow? first = this._resultsGrid.Rows[e.RowIndex1].Tag as ConversionRow;
        ConversionRow? second = this._resultsGrid.Rows[e.RowIndex2].Tag as ConversionRow;
        if (first is null || second is null)
        {
            return;
        }

        int result;
        switch (e.Column.Name)
        {
            case SourceInfoColumnName:
                result = first.Source.LastWriteTimeUtc.CompareTo(second.Source.LastWriteTimeUtc);
                break;
            case PdfStatusColumnName:
                result = CompareOutputs(first.GetOutput(OutputTarget.Pdf), second.GetOutput(OutputTarget.Pdf));
                break;
            case OdgStatusColumnName:
                result = CompareOutputs(first.GetOutput(OutputTarget.Odg), second.GetOutput(OutputTarget.Odg));
                break;
            case SlaStatusColumnName:
                result = CompareOutputs(first.GetOutput(OutputTarget.Sla), second.GetOutput(OutputTarget.Sla));
                break;
            case FolderColumnName:
                result = CompareFolderThenName(first.Source, second.Source);
                break;
            case FileNameColumnName:
                result = CompareNameThenFolder(first.Source, second.Source);
                break;
            default:
                return;
        }

        if (result == 0)
        {
            result = string.Compare(first.Source.FullPath, second.Source.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        e.SortResult = result;
        e.Handled = true;
    }

    // Folder and file name columns compare as plain text, ignoring case, with the other one as the
    // tie-breaker, so equal values always land in the same order.
    private static int CompareFolderThenName(SourceFile first, SourceFile second)
    {
        int result = string.Compare(first.SourceFolder, second.SourceFolder, StringComparison.OrdinalIgnoreCase);
        return result != 0
            ? result
            : string.Compare(first.FileName, second.FileName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNameThenFolder(SourceFile first, SourceFile second)
    {
        int result = string.Compare(first.FileName, second.FileName, StringComparison.OrdinalIgnoreCase);
        return result != 0
            ? result
            : string.Compare(first.SourceFolder, second.SourceFolder, StringComparison.OrdinalIgnoreCase);
    }

    // Missing outputs sort before existing ones (ascending), then existing ones by date, so one
    // click on a PDF/ODG/SLA header puts everything still missing at the top.
    private static int CompareOutputs(OutputInfo? first, OutputInfo? second)
    {
        return OutputSortKey(first).CompareTo(OutputSortKey(second));
    }

    private static long OutputSortKey(OutputInfo? output)
    {
        return output is { Exists: true } ? (output.LastWriteTimeUtc?.Ticks ?? 1L) : 0L;
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        this.ApplyFilter();
    }

    // Hides rows that do not match the filter. A row that gets hidden is also unticked, so
    // "Convert selected" can never act on a row the person cannot see.
    private void ApplyFilter()
    {
        string text = this._filterTextBox.Text.Trim();
        int mode = this._filterModeComboBox.SelectedIndex;

        this._resultsGrid.EndEdit();

        // A row that holds the current cell cannot be hidden, so clear it first.
        this._resultsGrid.CurrentCell = null;
        this._resultsGrid.ClearSelection();

        this._suspendHeaderUpdates = true;
        this._resultsGrid.SuspendLayout();

        int shown = 0;
        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            bool visible = gridRow.Tag is ConversionRow row && MatchesFilter(row, text, mode);
            if (!visible)
            {
                gridRow.Cells[SelectColumnName].Value = false;
            }

            gridRow.Visible = visible;
            if (visible)
            {
                shown++;
            }
        }

        this._resultsGrid.ResumeLayout();
        this._suspendHeaderUpdates = false;

        int total = this._resultsGrid.Rows.Count;
        this._filterCountLabel.Text = total == 0 ? string.Empty : $"Showing {shown} of {total} .pub files";
        this.UpdateHeaderCheckState();
    }

    private static bool MatchesFilter(ConversionRow row, string text, int mode)
    {
        if (text.Length > 0 && row.Source.FullPath.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return mode switch
        {
            1 => row.GetOutput(OutputTarget.Pdf) is not { Exists: true },
            2 => row.GetOutput(OutputTarget.Odg) is not { Exists: true },
            3 => row.GetOutput(OutputTarget.Sla) is not { Exists: true },
            4 => row.Outputs.Any(output => !output.Exists),
            5 => row.HasOutOfDateOutput,
            _ => true
        };
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
    private static void SetPendingStatus(DataGridViewRow gridRow, IReadOnlyList<OutputTarget> targets, string text)
    {
        if (targets.Contains(OutputTarget.Pdf))
        {
            gridRow.Cells[PdfStatusColumnName].Value = text;
            gridRow.Cells[PdfStatusColumnName].Style.ForeColor = Color.Empty;
        }

        if (targets.Contains(OutputTarget.Odg))
        {
            gridRow.Cells[OdgStatusColumnName].Value = text;
            gridRow.Cells[OdgStatusColumnName].Style.ForeColor = Color.Empty;
        }

        if (targets.Contains(OutputTarget.Sla))
        {
            gridRow.Cells[SlaStatusColumnName].Value = text;
            gridRow.Cells[SlaStatusColumnName].Style.ForeColor = Color.Empty;
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
        // format currently ticked under "Convert to" has no output file yet. Only rows that
        // are visible under the current filter are ticked.
        List<OutputTarget> wantedTargets = this.GetSelectedTargets();

        this._resultsGrid.EndEdit();
        this._suspendHeaderUpdates = true;

        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            bool needsWork = gridRow.Visible
                && gridRow.Tag is ConversionRow row
                && wantedTargets.Any(target => row.GetOutput(target) is not { Exists: true });
            gridRow.Cells[SelectColumnName].Value = needsWork;
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
            gridRow.Cells[SelectColumnName].Value = isChecked && gridRow.Visible;
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

        int total = 0;
        int ticked = 0;
        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            if (!gridRow.Visible)
            {
                continue;
            }

            total++;
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

    // A status cell shows the output's date and size when the file exists, and is blank when it
    // does not. The engine label (for LibreOffice and Scribus results) is appended after a
    // conversion, because libmspub output needs checking against the original. An output that
    // is older than its .pub starts with a warning sign and is drawn in a warning color; the
    // sign means the state is not carried by color alone.
    private static string FormatOutput(OutputInfo? output, string? engineLabel, bool outOfDate)
    {
        if (output is not { Exists: true })
        {
            return string.Empty;
        }

        string text = FormatDateAndSize(output.LastWriteTimeUtc ?? DateTime.MinValue, output.SizeBytes ?? 0);
        if (engineLabel is not null)
        {
            text = $"{text}  ({engineLabel})";
        }

        return outOfDate ? $"\u26A0 {text}" : text;
    }

    private static void SetStatusCell(DataGridViewCell cell, ConversionRow row, OutputTarget target, string? engineLabel)
    {
        OutputInfo? output = row.GetOutput(target);
        bool outOfDate = row.IsOutOfDate(target);

        cell.Value = FormatOutput(output, engineLabel, outOfDate);
        cell.Style.ForeColor = outOfDate ? GetOutOfDateColor() : Color.Empty;

        if (output is not { Exists: true })
        {
            cell.ToolTipText = string.Empty;
        }
        else if (outOfDate)
        {
            cell.ToolTipText = $"{output.ExpectedPath}\nOlder than the .pub: the .pub was changed after this was made.";
        }
        else
        {
            cell.ToolTipText = output.ExpectedPath;
        }
    }

    private static Color GetOutOfDateColor()
    {
        return Application.ColorMode == SystemColorMode.Dark
            ? Color.Orange
            : Color.FromArgb(190, 90, 0);
    }

    private static void SetFailedCell(DataGridViewCell cell, string? errorMessage)
    {
        cell.Value = $"Failed: {errorMessage}";
        cell.Style.ForeColor = Color.Empty;
        cell.ToolTipText = errorMessage ?? string.Empty;
    }

    // The same date-and-size text is used for the .pub column and the PDF, ODG and SLA columns.
    private static string FormatDateAndSize(DateTime utc, long bytes)
    {
        return $"{FormatDate(utc)}  {FormatSize(bytes)}";
    }

    private static string FormatDate(DateTime utc)
    {
        // The short date format from the person's Windows region settings (for example 10/5/2026
        // in the US, 05/10/2026 in the UK), so it reads the way they expect.
        return utc.ToLocalTime().ToString("d");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0} KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.00} GB";
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

        List<(DataGridViewRow GridRow, ConversionRow Row)> selected = this.GetSelectedRows();
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

        List<(DataGridViewRow GridRow, ConversionRow Row)> conflicts = selected
            .Where(item => wantedTargets.Any(target => item.Row.GetOutput(target) is { Exists: true }))
            .ToList();

        List<(DataGridViewRow GridRow, ConversionRow Row)> toConvert = selected;
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
                // Which of the chosen formats already have files, and how many, so the prompt can name them.
                List<(OutputTarget Target, int Count)> formatCounts = wantedTargets
                    .Select(target => (Target: target, Count: conflicts.Count(item => item.Row.GetOutput(target) is { Exists: true })))
                    .Where(item => item.Count > 0)
                    .ToList();

                using OverwriteConfirmationDialog dialog = new OverwriteConfirmationDialog(conflicts.Count, selected.Count, formatCounts);
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
        bool stoppedForTimeouts = false;

        foreach ((DataGridViewRow gridRow, ConversionRow row) in toConvert)
        {
            if (this._cancelRequested)
            {
                break;
            }

            SetPendingStatus(gridRow, wantedTargets, "Converting...");

            Task<RowConversionResult> conversionTask = Task.Run(() => rowConverter.Convert(row, wantedTargets));
            Task firstToFinish = await Task.WhenAny(conversionTask, Task.Delay(SlowConversionNoticeDelay));
            if (firstToFinish != conversionTask)
            {
                SetPendingStatus(gridRow, wantedTargets, SlowConversionNotice);
            }

            RowConversionResult result = await conversionTask;

            // The scan data is a snapshot, so re-read this row's outputs from disk now that the
            // engines have run. "Select not converted", sorting, filtering and the overwrite
            // check then all see the current state instead of the state at scan time.
            ConversionRow refreshed = RefreshRow(row);
            gridRow.Tag = refreshed;

            ConversionOutcome? pdfOutcome = result.ForTarget(OutputTarget.Pdf);
            if (pdfOutcome is not null)
            {
                if (pdfOutcome.Success)
                {
                    SetStatusCell(
                        gridRow.Cells[PdfStatusColumnName],
                        refreshed,
                        OutputTarget.Pdf,
                        usedOpenSourceEngine ? pdfEngineKind?.ToString() : null);
                    successCount++;
                }
                else
                {
                    SetFailedCell(gridRow.Cells[PdfStatusColumnName], pdfOutcome.ErrorMessage);
                    failureCount++;
                }
            }

            ConversionOutcome? odgOutcome = result.ForTarget(OutputTarget.Odg);
            if (odgOutcome is not null)
            {
                if (odgOutcome.Success)
                {
                    SetStatusCell(gridRow.Cells[OdgStatusColumnName], refreshed, OutputTarget.Odg, null);
                    successCount++;
                }
                else
                {
                    SetFailedCell(gridRow.Cells[OdgStatusColumnName], odgOutcome.ErrorMessage);
                    failureCount++;
                }
            }

            ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
            if (slaOutcome is not null)
            {
                if (slaOutcome.Success)
                {
                    SetStatusCell(gridRow.Cells[SlaStatusColumnName], refreshed, OutputTarget.Sla, "Scribus");
                    successCount++;
                }
                else
                {
                    SetFailedCell(gridRow.Cells[SlaStatusColumnName], slaOutcome.ErrorMessage);
                    failureCount++;
                }
            }

            gridRow.Cells[SelectColumnName].Value = false;
            processedCount++;

            // Several timeouts in a row mean Publisher itself is stuck, so stop the batch instead
            // of waiting out the time limit on every remaining file. Those rows stay ticked.
            if (publisherEngine.HasTimedOutRepeatedly)
            {
                stoppedForTimeouts = true;
                break;
            }
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

        if (stoppedForTimeouts)
        {
            int notTried = toConvert.Count - processedCount;
            summary += $" Stopped early: Publisher did not respond for {PublisherConversionEngine.MaxConsecutiveTimeouts} files in a row."
                + " Check that Publisher starts and is licensed (it may be waiting on a sign-in or license window),"
                + " or choose another engine.";
            if (notTried > 0)
            {
                summary += $" {notTried} file(s) were not tried and are still ticked.";
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

    // Ticked rows that are visible under the current filter, in the grid's current order.
    private List<(DataGridViewRow GridRow, ConversionRow Row)> GetSelectedRows()
    {
        List<(DataGridViewRow GridRow, ConversionRow Row)> selected = new List<(DataGridViewRow, ConversionRow)>();

        foreach (DataGridViewRow gridRow in this._resultsGrid.Rows)
        {
            if (gridRow.Visible
                && gridRow.Cells[SelectColumnName].Value is true
                && gridRow.Tag is ConversionRow row)
            {
                selected.Add((gridRow, row));
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
