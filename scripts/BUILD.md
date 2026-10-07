# Building and releasing AfterPub

This folder holds `Release.ps1`, which builds, tests and packages the portable Windows
program, and can optionally tag the release and publish it on GitHub. Run it from the
repository root in PowerShell.

## Before you start

- Windows with the [.NET 10 SDK](https://dotnet.microsoft.com/download) and git.
- PowerShell must be allowed to run local scripts. If it refuses, run this once:
  `Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned`
  (and `Unblock-File .\scripts\Release.ps1` if the file was downloaded).
- `artifacts/` must be listed in `.gitignore`. The script writes its output there.
- Everything must be committed and pushed. The script refuses to run otherwise, so the
  program is always built from exactly the commit that gets tagged.
- Only for `-Publish`: the GitHub CLI (`winget install GitHub.cli`, then `gh auth login`).

## The stages

| Stage | What happens | Command |
|---|---|---|
| 1. Check | Version read from `Directory.Build.props`; git must be clean, pushed and in sync; the tag must not exist yet | every run |
| 2. Build and test | `dotnet test -c Release`, then a single-file, self-contained win-x64 publish into `artifacts\publish` | every run |
| 3. Verify | The publish folder must hold only `AfterPub.exe`, and its version must start with the `<Version>` number | every run |
| 4. Package | `artifacts\AfterPub-<version>-win-x64.zip`, a `.sha256` file and `release-notes-v<version>.md` | every run |
| 5. Tag and push | Pauses so you can test the exe, asks you to type `yes`, then creates `v<version>` and pushes it | `-Tag` or `-Publish` |
| 6. GitHub release | Creates the release, attaches the zip and checksum, marks it a pre-release if the version has a hyphen | `-Publish` only |

Stages 1 to 4 can be run as often as you like. They change nothing in git: each run
deletes and rebuilds the contents of `artifacts`. A rebuilt zip has a different checksum
each time (zip files contain timestamps), so always use the zip and notes from the same run.

Stage 5 is the point of no return. Once a tag is pushed, the script will refuse to reuse
that version.

## Switches

| Switch | Effect |
|---|---|
| (none) | Stages 1 to 4 only. Safe. |
| `-Tag` | Also stage 5. Create the GitHub release yourself on the website (see below). |
| `-Publish` | Stages 5 and 6 as well. Includes `-Tag`. |
| `-Yes` | Skip the "type yes" question. |
| `-SkipTests` | Skip `dotnet test`. Not recommended for a release. |
| `-NotesFile path.md` | Use your own release notes instead of the generated ones. |

## A normal release

1. Set the version: edit `<Version>` in `Directory.Build.props` (for example `1.0.0-rc.2`,
   or `1.0.0` for the final release). Commit and push.
2. `.\scripts\Release.ps1` to build and package. Fix anything it reports and repeat.
3. Test `artifacts\publish\AfterPub.exe`: start it, press F1 to check the version, scan a folder.
4. `.\scripts\Release.ps1 -Tag` (or `-Publish`). It rebuilds, pauses, and waits for `yes`.
5. Publish the release (automatically with `-Publish`, or by hand, below).
6. Download the zip from the release page on a different PC. Check the checksum with
   `Get-FileHash <file> -Algorithm SHA256`, and see what Windows SmartScreen shows.

## Creating the GitHub release by hand (after `-Tag`)

The tag is already on GitHub. Wording on the site may differ slightly.

1. Open the repository's **Releases** page and choose **Draft a new release**.
2. Under the tag chooser, pick the existing tag `v<version>`. Do not create a new one.
3. Set the title to `AfterPub <version>`.
4. Paste the contents of `artifacts\release-notes-v<version>.md` into the description, and
   edit it if you like.
5. Attach `artifacts\AfterPub-<version>-win-x64.zip` and its `.sha256` file.
6. For a version with a hyphen (a release candidate), tick **Set as a pre-release**.
7. Save it as a draft and look it over, then publish.

Use the zip from the same run as the notes you pasted, because the checksum in the notes
belongs to that exact zip.

## If something goes wrong

- **The script stops with "RELEASE STOPPED".** The message says why. Nothing is tagged or
  pushed until the `yes` question, and the script stops on any failed step.
- **The tag was created locally but the push failed.** Remove the local tag and rerun:
  `git tag -d v<version>`
- **A tag or release was published with a mistake.** If nobody could have downloaded it yet,
  delete it:
  `git push origin --delete v<version>` and `git tag -d v<version>`, then delete the release on
  the Releases page (or `gh release delete v<version>`). If anyone might have downloaded it,
  do not reuse the number: raise the version (`rc.2`) and release again.
- **"Tag already exists".** Raise `<Version>` in `Directory.Build.props`, commit and push.
- **"uncommitted or untracked files".** Commit them, or add the folder to `.gitignore`.

## Notes

- The program is not code-signed, so Windows SmartScreen may warn on first run. The release
  notes and the README say so.
- Trimming is deliberately off: the Publisher engine uses COM late binding, which trimming
  can break.
- Only Windows 10/11 x64 is built. To change that, edit the `-r win-x64` arguments in
  `Release.ps1`.
