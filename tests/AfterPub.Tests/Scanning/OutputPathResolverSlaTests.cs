using AfterPub.Core.Scanning;

namespace AfterPub.Tests.Scanning;

public sealed class OutputPathResolverSlaTests
{
    [Fact]
    public void ResolveOutputPath_Sla_SameFolder_UsesSlaExtensionNextToSource()
    {
        SourceFile source = new SourceFile(@"C:\work\flyer.pub", "flyer.pub", 0, DateTime.UtcNow);
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\work",
            EnabledTargets = new[] { OutputTarget.Sla }
        };

        string path = new OutputPathResolver().ResolveOutputPath(source, OutputTarget.Sla, options);

        Assert.Equal(@"C:\work\flyer.sla", path);
    }

    [Fact]
    public void ResolveOutputPath_Sla_SeparateFolder_MirrorsSourceStructure()
    {
        SourceFile source = new SourceFile(@"C:\work\Sub\flyer.pub", @"Sub\flyer.pub", 0, DateTime.UtcNow);
        ScanOptions options = new ScanOptions
        {
            RootFolder = @"C:\work",
            EnabledTargets = new[] { OutputTarget.Sla },
            LocationMode = OutputLocationMode.SeparateFolder,
            SeparateOutputRoot = @"D:\out"
        };

        string path = new OutputPathResolver().ResolveOutputPath(source, OutputTarget.Sla, options);

        Assert.Equal(@"D:\out\Sub\flyer.sla", path);
    }
}
