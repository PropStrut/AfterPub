using AfterPub.Core.Engines;
using Xunit;

namespace AfterPub.Tests.Engines;

public class LibreOfficeConversionEngineTests
{
    [Fact]
    public void ResolveExecutablePath_PrefersConfiguredPath_WhenItExists()
    {
        string configuredPath = @"D:\Custom\soffice.exe";
        string[] standardPaths = { @"C:\Program Files\LibreOffice\program\soffice.exe" };

        string? result = LibreOfficeConversionEngine.ResolveExecutablePath(
            configuredPath,
            standardPaths,
            path => path == configuredPath);

        Assert.Equal(configuredPath, result);
    }

    [Fact]
    public void ResolveExecutablePath_FallsBackToStandardPaths_WhenConfiguredPathMissing()
    {
        string configuredPath = @"D:\Custom\soffice.exe";
        string standardPath = @"C:\Program Files\LibreOffice\program\soffice.exe";
        string[] standardPaths = { standardPath };

        string? result = LibreOfficeConversionEngine.ResolveExecutablePath(
            configuredPath,
            standardPaths,
            path => path == standardPath);

        Assert.Equal(standardPath, result);
    }

    [Fact]
    public void ResolveExecutablePath_IgnoresBlankConfiguredPath()
    {
        string standardPath = @"C:\Program Files\LibreOffice\program\soffice.exe";
        string[] standardPaths = { standardPath };

        string? result = LibreOfficeConversionEngine.ResolveExecutablePath(
            configuredPath: "   ",
            standardPaths,
            path => path == standardPath);

        Assert.Equal(standardPath, result);
    }

    [Fact]
    public void ResolveExecutablePath_ReturnsNull_WhenNothingExists()
    {
        string[] standardPaths =
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        string? result = LibreOfficeConversionEngine.ResolveExecutablePath(
            configuredPath: null,
            standardPaths,
            path => false);

        Assert.Null(result);
    }
}
