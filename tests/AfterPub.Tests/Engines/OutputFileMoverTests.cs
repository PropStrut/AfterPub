using AfterPub.Core.Engines;
using Xunit;

namespace AfterPub.Tests.Engines;

public class OutputFileMoverTests : IDisposable
{
    private readonly string _tempRoot;

    public OutputFileMoverTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubMoverTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(this._tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    [Fact]
    public void MoveIntoPlace_MovesTheFile_AndRemovesTheTemporaryOne()
    {
        string temp = this.WriteFile(Path.Combine("work", "output.pdf"), "NEW");
        string final = Path.Combine(this._tempRoot, "out", "Flyer.pdf");

        OutputFileMover.MoveIntoPlace(temp, final);

        Assert.Equal("NEW", File.ReadAllText(final));
        Assert.False(File.Exists(temp));
    }

    [Fact]
    public void MoveIntoPlace_ReplacesAnExistingFile()
    {
        string temp = this.WriteFile(Path.Combine("work", "output.pdf"), "NEW");
        string final = this.WriteFile(Path.Combine("out", "Flyer.pdf"), "OLD");

        OutputFileMover.MoveIntoPlace(temp, final);

        Assert.Equal("NEW", File.ReadAllText(final));
    }

    [Fact]
    public void MoveIntoPlace_CreatesTheFinalFolder_WhenItDoesNotExist()
    {
        string temp = this.WriteFile(Path.Combine("work", "output.pdf"), "NEW");
        string final = Path.Combine(this._tempRoot, "Mirrored", "Sub", "Flyer.pdf");

        OutputFileMover.MoveIntoPlace(temp, final);

        Assert.True(File.Exists(final));
    }

    [Fact]
    public void MoveIntoPlace_LeavesNoTemporaryNameBehind()
    {
        string temp = this.WriteFile(Path.Combine("work", "output.pdf"), "NEW");
        string final = Path.Combine(this._tempRoot, "out", "Flyer.pdf");

        OutputFileMover.MoveIntoPlace(temp, final);

        string[] leftovers = Directory.GetFiles(Path.Combine(this._tempRoot, "out"), "*" + OutputFileMover.StagingSuffix);
        Assert.Empty(leftovers);
    }

    [Fact]
    public void MoveIntoPlace_KeepsTheExistingFile_WhenTheTemporaryFileIsMissing()
    {
        string final = this.WriteFile(Path.Combine("out", "Flyer.pdf"), "OLD");
        string missing = Path.Combine(this._tempRoot, "work", "output.pdf");

        Assert.ThrowsAny<IOException>(() => OutputFileMover.MoveIntoPlace(missing, final));

        Assert.Equal("OLD", File.ReadAllText(final));
    }
}
