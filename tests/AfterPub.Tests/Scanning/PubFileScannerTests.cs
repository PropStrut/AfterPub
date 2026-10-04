using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Scanning;

public class PubFileScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public PubFileScannerTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    private string WriteFile(string relativePath, string content = "placeholder")
    {
        string fullPath = Path.Combine(this._tempRoot, relativePath);
        string? folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    [Fact]
    public void Scan_FindsPubFileInTopFolder()
    {
        this.WriteFile("Flyer.pub");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf }
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Single(rows);
        Assert.Equal("Flyer.pub", rows[0].Source.FileName);
    }

    [Fact]
    public void Scan_IgnoresSubfolder_WhenNotRecursive()
    {
        this.WriteFile("Top.pub");
        this.WriteFile(Path.Combine("Sub", "Nested.pub"));
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf }
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Single(rows);
        Assert.Equal("Top.pub", rows[0].Source.FileName);
    }

    [Fact]
    public void Scan_FindsSubfolderFiles_WhenRecursive()
    {
        this.WriteFile("Top.pub");
        this.WriteFile(Path.Combine("Sub", "Nested.pub"));
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = true,
            EnabledTargets = new[] { OutputTarget.Pdf }
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Scan_IgnoresNonPubFiles()
    {
        this.WriteFile("Flyer.pub");
        this.WriteFile("Flyer.pdf");
        this.WriteFile("readme.txt");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf }
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Single(rows);
    }

    [Fact]
    public void Scan_RowIsFullyConverted_WhenPdfExistsNextToSource()
    {
        this.WriteFile("Flyer.pub");
        this.WriteFile("Flyer.pdf");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SameFolder
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.True(rows[0].IsFullyConverted);
        Assert.True(rows[0].GetOutput(OutputTarget.Pdf)!.Exists);
    }

    [Fact]
    public void Scan_RowIsNotFullyConverted_WhenNoOutputExists()
    {
        this.WriteFile("Flyer.pub");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SameFolder
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.False(rows[0].IsFullyConverted);
    }

    [Fact]
    public void Scan_RowIsNotFullyConverted_WhenOnlySomeEnabledTargetsExist()
    {
        this.WriteFile("Flyer.pub");
        this.WriteFile("Flyer.pdf");
        // No .odg written, but Odg is an enabled target below.
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false,
            EnabledTargets = new[] { OutputTarget.Pdf, OutputTarget.Odg },
            LocationMode = OutputLocationMode.SameFolder
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.False(rows[0].IsFullyConverted);
        Assert.True(rows[0].GetOutput(OutputTarget.Pdf)!.Exists);
        Assert.False(rows[0].GetOutput(OutputTarget.Odg)!.Exists);
    }

    [Fact]
    public void Scan_ThrowsDirectoryNotFoundException_WhenRootFolderDoesNotExist()
    {
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = Path.Combine(this._tempRoot, "DoesNotExist"),
            EnabledTargets = new[] { OutputTarget.Pdf }
        };

        Assert.Throws<DirectoryNotFoundException>(() => scanner.Scan(options));
    }

    [Fact]
    public void Scan_ReportsAllTargets_WhenEnabledTargetsNotSet()
    {
        this.WriteFile("Flyer.pub");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Single(rows);
        Assert.Equal(3, rows[0].Outputs.Count);
        Assert.NotNull(rows[0].GetOutput(OutputTarget.Pdf));
        Assert.NotNull(rows[0].GetOutput(OutputTarget.Odg));
        Assert.NotNull(rows[0].GetOutput(OutputTarget.Sla));
    }

    [Fact]
    public void Scan_ReportsExistingDerivativeFiles_ForEachTarget()
    {
        this.WriteFile("Flyer.pub");
        this.WriteFile("Flyer.pdf");
        this.WriteFile("Flyer.sla");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.True(rows[0].GetOutput(OutputTarget.Pdf)!.Exists);
        Assert.False(rows[0].GetOutput(OutputTarget.Odg)!.Exists);
        Assert.True(rows[0].GetOutput(OutputTarget.Sla)!.Exists);
    }

    [Fact]
    public void Scan_IncludesHiddenFilesAndFolders()
    {
        string hiddenFile = this.WriteFile("Hidden.pub");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        this.WriteFile(Path.Combine("HiddenFolder", "InHidden.pub"));
        DirectoryInfo hiddenFolder = new DirectoryInfo(Path.Combine(this._tempRoot, "HiddenFolder"));
        hiddenFolder.Attributes |= FileAttributes.Hidden;

        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = true
        };

        IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Source.FileName == "Hidden.pub");
        Assert.Contains(rows, row => row.Source.FileName == "InHidden.pub");
    }

    [Fact]
    public void Scan_Recursive_SkipsSystemFolders()
    {
        this.WriteFile("Visible.pub");
        this.WriteFile(Path.Combine("SystemFolder", "Buried.pub"));
        DirectoryInfo systemFolder = new DirectoryInfo(Path.Combine(this._tempRoot, "SystemFolder"));
        systemFolder.Attributes |= FileAttributes.System;

        try
        {
            PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
            ScanOptions options = new ScanOptions
            {
                RootFolder = this._tempRoot,
                Recursive = true
            };

            IReadOnlyList<ConversionRow> rows = scanner.Scan(options);

            Assert.Single(rows);
            Assert.Equal("Visible.pub", rows[0].Source.FileName);
        }
        finally
        {
            // Clear the attribute so the test's cleanup can always delete the folder.
            systemFolder.Attributes &= ~FileAttributes.System;
        }
    }

    [Fact]
    public void Scan_ReportsProgress_AfterEachFileFound()
    {
        this.WriteFile("One.pub");
        this.WriteFile("Two.pub");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false
        };
        RecordingProgress progress = new RecordingProgress();

        scanner.Scan(options, CancellationToken.None, progress);

        Assert.Equal(new[] { 1, 2 }, progress.Values);
    }

    [Fact]
    public void Scan_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled()
    {
        this.WriteFile("Flyer.pub");
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions
        {
            RootFolder = this._tempRoot,
            Recursive = false
        };
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => scanner.Scan(options, cancellation.Token));
    }

    // Collects reported values synchronously. Progress<T> posts to another thread, which would
    // make the assertions above race with the scan.
    private sealed class RecordingProgress : IProgress<int>
    {
        public List<int> Values { get; } = new List<int>();

        public void Report(int value)
        {
            this.Values.Add(value);
        }
    }
}
