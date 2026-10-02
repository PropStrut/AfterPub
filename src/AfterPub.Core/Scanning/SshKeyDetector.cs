using System.Text;

namespace AfterPub.Core.Scanning;

/// <summary>
/// Recognizes SSH public key files, which also use the .pub extension but are not
/// Publisher documents. Identifies them by content, not by folder, so a key copied
/// out of a .ssh folder (a backup, a downloads folder, a repo) is still recognized.
/// Read-only: it opens the file for reading and never modifies it (CLAUDE.md section 13).
/// </summary>
public static class SshKeyDetector
{
    // Enough bytes to cover the longest text marker below, with room to spare.
    private const int HeaderLength = 64;

    // Publisher documents are OLE compound files and begin with this signature.
    private static readonly byte[] _oleSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    private static readonly byte[] _utf8Bom = { 0xEF, 0xBB, 0xBF };

    // Text a key file begins with: OpenSSH types (ssh-rsa, ssh-ed25519, ssh-dss, ecdsa-sha2-*,
    // and the sk-* hardware-key types), PEM public keys, and the RFC 4716 format PuTTYgen exports.
    private static readonly string[] _keyPrefixes =
    {
        "ssh-",
        "ecdsa-sha2-",
        "sk-ssh-",
        "sk-ecdsa-",
        "-----BEGIN ",
        "---- BEGIN SSH2 PUBLIC KEY"
    };

    /// <summary>
    /// True if the file at <paramref name="path"/> looks like an SSH public key. Returns false
    /// when the file cannot be read, so an unreadable file still shows up in the list and fails
    /// visibly at conversion time instead of disappearing silently.
    /// </summary>
    public static bool IsSshPublicKey(string path)
    {
        byte[] buffer = new byte[HeaderLength];
        int bytesRead;

        try
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            bytesRead = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return LooksLikeSshKey(new ReadOnlySpan<byte>(buffer, 0, bytesRead));
    }

    /// <summary>Content check on the first bytes of a file; separate from file access so it is easy to test.</summary>
    public static bool LooksLikeSshKey(ReadOnlySpan<byte> header)
    {
        // A real Publisher (OLE) file is never a key, whatever else it contains.
        if (header.StartsWith(_oleSignature))
        {
            return false;
        }

        if (header.StartsWith(_utf8Bom))
        {
            header = header.Slice(_utf8Bom.Length);
        }

        string text = Encoding.ASCII.GetString(header).TrimStart();

        foreach (string prefix in _keyPrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
