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
}
