using System.Text.RegularExpressions;

namespace AfterPub.Core.Engines;

/// <summary>
/// Finds Scribus.exe. Checks a path from settings first, then the standard install
/// locations (CLAUDE.md section 3.1). The Windows installer puts Scribus in a folder with
/// the version in its name (for example "Scribus 1.6.6"), so this searches for folders
/// starting with "Scribus" and picks the highest version, rather than hard-coding one name.
/// </summary>
public sealed class ScribusLocator
{
    private const string ExecutableName = "Scribus.exe";
    private const string FolderPattern = "Scribus*";

    // Matches "1.6.6", "1.10.0", and similar, anywhere in a folder name.
    private static readonly Regex _versionPattern = new Regex(@"\d+(\.\d+){1,3}", RegexOptions.Compiled);

    private readonly IReadOnlyList<string> _searchRoots;

    /// <summary>Uses the Program Files folders as the search roots.</summary>
    public ScribusLocator()
        : this(GetDefaultSearchRoots())
    {
    }

    /// <summary>Uses the given search roots. Lets tests point the search at temporary folders.</summary>
    public ScribusLocator(IReadOnlyList<string> searchRoots)
    {
        this._searchRoots = searchRoots;
    }

    /// <summary>
    /// Returns the full path to Scribus.exe, or null if none was found. A configured path
    /// that exists wins; a configured path that does not exist is ignored and the standard
    /// locations are searched instead.
    /// </summary>
    public string? Locate(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        string? bestPath = null;
        Version? bestVersion = null;

        foreach (string root in this._searchRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string folder in Directory.EnumerateDirectories(root, FolderPattern))
            {
                string candidate = Path.Combine(folder, ExecutableName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                Version? version = ParseVersion(Path.GetFileName(folder));
                if (bestPath is null || IsNewer(version, bestVersion))
                {
                    bestPath = candidate;
                    bestVersion = version;
                }
            }
        }

        return bestPath;
    }

    // Compare as numbers, not text, so 1.10.0 beats 1.6.6. A folder with no readable
    // version still counts, but any versioned folder beats it.
    private static bool IsNewer(Version? candidate, Version? current)
    {
        if (candidate is null)
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        return candidate > current;
    }

    private static Version? ParseVersion(string folderName)
    {
        Match match = _versionPattern.Match(folderName);
        if (match.Success && Version.TryParse(match.Value, out Version? version))
        {
            return version;
        }

        return null;
    }

    private static IReadOnlyList<string> GetDefaultSearchRoots()
    {
        List<string> roots = new List<string>();
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        return roots;
    }

    private static void AddRoot(List<string> roots, string path)
    {
        // The two Program Files folders are the same on a 32-bit system, so skip duplicates.
        if (!string.IsNullOrEmpty(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            roots.Add(path);
        }
    }
}
