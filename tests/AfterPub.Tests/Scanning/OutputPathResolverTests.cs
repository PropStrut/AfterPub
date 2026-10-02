using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Scanning;

public class OutputPathResolverTests
{
    private static SourceFile MakeSource(string sourceFolder, string relativePath, string baseFileName)
    {
        string fullPath = Path.Combine(sourceFolder, relativePath);
        return new SourceFile(fullPath, relativePath, sizeBytes: 1024, lastWriteTimeUtc: DateTime.UtcNow);
    }

    [Fact]
    public void ResolveOutputPath_SameFolder_PlacesOutputNextToSource()
    {
        OutputPathResolver resolver = new OutputPathResolver();
        SourceFile source = MakeSource(@"C:\Docs", "Flyer.pub", "Flyer");
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\Docs",
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SameFolder
        };

        string result = resolver.ResolveOutputPath(source, OutputTarget.Pdf, options);

        Assert.Equal(Path.Combine(@"C:\Docs", "Flyer.pdf"), result);
    }

    [Fact]
    public void ResolveOutputPath_SameFolder_UsesOdgExtensionForOdgTarget()
    {
        OutputPathResolver resolver = new OutputPathResolver();
        SourceFile source = MakeSource(@"C:\Docs", "Flyer.pub", "Flyer");
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\Docs",
            EnabledTargets = new[] { OutputTarget.Odg },
            LocationMode = OutputLocationMode.SameFolder
        };

        string result = resolver.ResolveOutputPath(source, OutputTarget.Odg, options);

        Assert.Equal(Path.Combine(@"C:\Docs", "Flyer.odg"), result);
    }

    [Fact]
    public void ResolveOutputPath_SeparateFolder_MirrorsSourceSubfolder()
    {
        OutputPathResolver resolver = new OutputPathResolver();
        string relativePath = Path.Combine("A", "B", "Flyer.pub");
        SourceFile source = MakeSource(@"C:\Docs", relativePath, "Flyer");
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\Docs",
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SeparateFolder,
            SeparateOutputRoot = @"D:\Output"
        };

        string result = resolver.ResolveOutputPath(source, OutputTarget.Pdf, options);

        string expected = Path.Combine(@"D:\Output", "A", "B", "Flyer.pdf");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveOutputPath_SeparateFolder_TwoSourcesWithSameNameInDifferentFolders_DoNotCollide()
    {
        OutputPathResolver resolver = new OutputPathResolver();
        SourceFile sourceA = MakeSource(@"C:\Docs", Path.Combine("A", "Flyer.pub"), "Flyer");
        SourceFile sourceB = MakeSource(@"C:\Docs", Path.Combine("B", "Flyer.pub"), "Flyer");
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\Docs",
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SeparateFolder,
            SeparateOutputRoot = @"D:\Output"
        };

        string resultA = resolver.ResolveOutputPath(sourceA, OutputTarget.Pdf, options);
        string resultB = resolver.ResolveOutputPath(sourceB, OutputTarget.Pdf, options);

        Assert.NotEqual(resultA, resultB);
    }

    [Fact]
    public void ResolveOutputPath_SeparateFolder_WithoutSeparateOutputRoot_Throws()
    {
        OutputPathResolver resolver = new OutputPathResolver();
        SourceFile source = MakeSource(@"C:\Docs", "Flyer.pub", "Flyer");
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\Docs",
            EnabledTargets = new[] { OutputTarget.Pdf },
            LocationMode = OutputLocationMode.SeparateFolder,
            SeparateOutputRoot = null
        };

        Assert.Throws<InvalidOperationException>(() =>
            resolver.ResolveOutputPath(source, OutputTarget.Pdf, options));
    }
}
