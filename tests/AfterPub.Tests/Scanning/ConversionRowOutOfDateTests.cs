using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Scanning;

public class ConversionRowOutOfDateTests : IDisposable
{
    private static readonly DateTime BaseTimeUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _tempRoot;

    public ConversionRowOutOfDateTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubOutOfDateTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    // Writes Flyer.pub (and Flyer.pdf when pdfOffset is given) with the .pub at BaseTimeUtc and
    // the PDF at BaseTimeUtc + pdfOffset, then scans and returns the single row.
    private ConversionRow ScanFlyer(TimeSpan? pdfOffset)
    {
        string pubPath = Path.Combine(this._tempRoot, "Flyer.pub");
        File.WriteAllText(pubPath, "placeholder");
        File.SetLastWriteTimeUtc(pubPath, BaseTimeUtc);

        if (pdfOffset is not null)
        {
            string pdfPath = Path.Combine(this._tempRoot, "Flyer.pdf");
            File.WriteAllText(pdfPath, "placeholder");
            File.SetLastWriteTimeUtc(pdfPath, BaseTimeUtc + pdfOffset.Value);
        }

        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions { RootFolder = this._tempRoot, Recursive = false };
        return Assert.Single(scanner.Scan(options));
    }

    [Fact]
    public void IsOutOfDate_True_WhenSourceIsNewerThanOutput()
    {
        ConversionRow row = this.ScanFlyer(TimeSpan.FromDays(-3));

        Assert.True(row.IsOutOfDate(OutputTarget.Pdf));
        Assert.True(row.HasOutOfDateOutput);
    }

    [Fact]
    public void IsOutOfDate_False_WhenOutputIsNewerThanSource()
    {
        ConversionRow row = this.ScanFlyer(TimeSpan.FromDays(3));

        Assert.False(row.IsOutOfDate(OutputTarget.Pdf));
        Assert.False(row.HasOutOfDateOutput);
    }

    [Fact]
    public void IsOutOfDate_False_WhenDifferenceIsWithinTolerance()
    {
        ConversionRow row = this.ScanFlyer(TimeSpan.FromSeconds(-1));

        Assert.False(row.IsOutOfDate(OutputTarget.Pdf));
    }

    [Fact]
    public void IsOutOfDate_False_WhenOutputIsMissing()
    {
        ConversionRow row = this.ScanFlyer(pdfOffset: null);

        Assert.False(row.IsOutOfDate(OutputTarget.Pdf));
        Assert.False(row.HasOutOfDateOutput);
    }

    [Fact]
    public void IsOutOfDate_IsPerTarget()
    {
        ConversionRow row = this.ScanFlyer(TimeSpan.FromDays(-3));

        Assert.True(row.IsOutOfDate(OutputTarget.Pdf));
        Assert.False(row.IsOutOfDate(OutputTarget.Odg));
    }
}
