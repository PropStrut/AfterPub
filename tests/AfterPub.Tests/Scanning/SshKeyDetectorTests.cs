using System.Text;
using AfterPub.Core.Scanning;

namespace AfterPub.Tests.Scanning;

public sealed class SshKeyDetectorTests
{
    [Theory]
    [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5 user@host")]
    [InlineData("ssh-rsa AAAAB3NzaC1yc2E user@host")]
    [InlineData("ecdsa-sha2-nistp256 AAAAE2VjZHNh user@host")]
    [InlineData("sk-ssh-ed25519@openssh.com AAAAGnNr user@host")]
    [InlineData("-----BEGIN PUBLIC KEY-----")]
    [InlineData("---- BEGIN SSH2 PUBLIC KEY ----")]
    [InlineData("\r\n  ssh-rsa AAAAB3NzaC1yc2E user@host")]
    public void LooksLikeSshKey_KeyFormats_ReturnsTrue(string content)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(content);

        Assert.True(SshKeyDetector.LooksLikeSshKey(bytes));
    }

    [Fact]
    public void LooksLikeSshKey_Utf8BomBeforeKey_ReturnsTrue()
    {
        byte[] bom = { 0xEF, 0xBB, 0xBF };
        byte[] key = Encoding.ASCII.GetBytes("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5 user@host");
        byte[] bytes = bom.Concat(key).ToArray();

        Assert.True(SshKeyDetector.LooksLikeSshKey(bytes));
    }

    [Fact]
    public void LooksLikeSshKey_OleSignature_ReturnsFalse()
    {
        byte[] bytes = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00 };

        Assert.False(SshKeyDetector.LooksLikeSshKey(bytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hello, this is some other text file")]
    public void LooksLikeSshKey_OtherContent_ReturnsFalse(string content)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(content);

        Assert.False(SshKeyDetector.LooksLikeSshKey(bytes));
    }

    [Fact]
    public void IsSshPublicKey_KeyFile_ReturnsTrueAndMissingFileReturnsFalse()
    {
        string path = Path.Combine(Path.GetTempPath(), "AfterPubTest_" + Guid.NewGuid().ToString("N") + ".pub");

        try
        {
            File.WriteAllText(path, "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5 user@host");

            Assert.True(SshKeyDetector.IsSshPublicKey(path));
        }
        finally
        {
            File.Delete(path);
        }

        // The file is gone now: an unreadable file must not be treated as a key.
        Assert.False(SshKeyDetector.IsSshPublicKey(path));
    }
}
