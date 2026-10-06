<#
.SYNOPSIS
    Builds, tests and packages an AfterPub release. Optionally tags, pushes and publishes it.

.DESCRIPTION
    Run it from anywhere:

        .\scripts\Release.ps1                 Build, test and package only. Safe: changes nothing in git.
        .\scripts\Release.ps1 -Tag            Also create the version tag and push it.
        .\scripts\Release.ps1 -Publish        Also create the GitHub release (needs the gh tool).
        .\scripts\Release.ps1 -SkipTests      Skip "dotnet test" (not recommended for a release).
        .\scripts\Release.ps1 -Publish -Yes   Skip the "type yes" question before tagging.
        .\scripts\Release.ps1 -NotesFile x.md Use your own release notes instead of the default.

    The version comes from <Version> in Directory.Build.props; the tag is "v" plus that version.
    Everything must be committed and pushed first, so the exe is built from exactly the commit
    that gets tagged. Output goes to the "artifacts" folder, which must be listed in .gitignore.

    Layout assumed: this file is in <repo root>\scripts\. If you move it to the repo root,
    change the $repoRoot line below to:  $repoRoot = $PSScriptRoot
#>
[CmdletBinding()]
param(
    [switch]$Tag,
    [switch]$Publish,
    [switch]$SkipTests,
    [switch]$Yes,
    [string]$NotesFile
)

function Write-Step
{
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Runs a program and stops the whole script if it reports failure.
function Invoke-Native
{
    param(
        [string]$Description,
        [string]$Program,
        [string[]]$Arguments
    )

    Write-Step $Description
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "$Description failed (exit code $LASTEXITCODE)."
    }
}

# Runs git and returns its output as one trimmed string.
function Get-GitText
{
    param([string[]]$Arguments)

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "git $($Arguments -join ' ') failed."
    }

    return [string]::Join("`n", @($output)).Trim()
}

function Write-Utf8File
{
    param(
        [string]$Path,
        [string]$Text
    )

    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try
{
    $doTag = $Tag -or $Publish

    # --- Version -------------------------------------------------------------------------
    [xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
    $versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText))
    {
        throw 'Could not find <Version> in Directory.Build.props.'
    }

    [string]$version = $versionNode.InnerText.Trim()
    [string]$tagName = "v$version"
    [bool]$isPreRelease = $version.Contains('-')

    Write-Host "AfterPub release $version (tag $tagName)" -ForegroundColor Green

    # --- Git state -----------------------------------------------------------------------
    Write-Step 'Checking the git state'
    if ($null -eq (Get-Command git -ErrorAction SilentlyContinue))
    {
        throw 'git was not found on PATH.'
    }

    $dirty = Get-GitText @('status', '--porcelain')
    if ($dirty.Length -gt 0)
    {
        throw "There are uncommitted or untracked files. Commit them (or add the folder to .gitignore) first:`n$dirty"
    }

    Invoke-Native 'Fetching from origin' 'git' @('fetch', '--quiet')

    & git rev-parse --abbrev-ref '@{u}' *> $null
    if ($LASTEXITCODE -ne 0)
    {
        throw 'The current branch has no upstream branch on GitHub. Push it first.'
    }

    [int]$ahead = [int](Get-GitText @('rev-list', '--count', '@{u}..HEAD'))
    [int]$behind = [int](Get-GitText @('rev-list', '--count', 'HEAD..@{u}'))
    if ($ahead -gt 0)
    {
        throw "There are $ahead local commit(s) not pushed to GitHub. Push them first."
    }

    if ($behind -gt 0)
    {
        throw "GitHub has $behind commit(s) this checkout does not. Pull them first."
    }

    $branch = Get-GitText @('rev-parse', '--abbrev-ref', 'HEAD')
    $commit = Get-GitText @('rev-parse', '--short', 'HEAD')
    Write-Host "Branch $branch, commit $commit, clean and in sync with GitHub."

    if ($doTag)
    {
        if ((Get-GitText @('tag', '--list', $tagName)).Length -gt 0)
        {
            throw "Tag $tagName already exists locally. Raise <Version> in Directory.Build.props first."
        }

        if ((Get-GitText @('ls-remote', '--tags', 'origin', $tagName)).Length -gt 0)
        {
            throw "Tag $tagName already exists on GitHub. Raise <Version> in Directory.Build.props first."
        }
    }

    if ($Publish)
    {
        if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue))
        {
            throw 'The GitHub CLI (gh) was not found. Install it with "winget install GitHub.cli", then run "gh auth login".'
        }

        & gh auth status *> $null
        if ($LASTEXITCODE -ne 0)
        {
            throw 'The GitHub CLI is not signed in. Run "gh auth login" first.'
        }
    }

    # --- Tests ---------------------------------------------------------------------------
    if (-not $SkipTests)
    {
        Invoke-Native 'Running the tests' 'dotnet' @('test', '-c', 'Release')
    }

    # --- Publish -------------------------------------------------------------------------
    $artifacts = Join-Path $repoRoot 'artifacts'
    $publishDir = Join-Path $artifacts 'publish'
    if (Test-Path -LiteralPath $publishDir)
    {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Invoke-Native 'Publishing the portable exe (win-x64)' 'dotnet' @(
        'publish', 'src/AfterPub.App',
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-o', $publishDir)

    Write-Step 'Checking the published output'
    $exe = Join-Path $publishDir 'AfterPub.exe'
    $items = @(Get-ChildItem -LiteralPath $publishDir)
    if ($items.Count -ne 1 -or $items[0].Name -ne 'AfterPub.exe')
    {
        $names = ($items | ForEach-Object { $_.Name }) -join ', '
        throw "Expected the publish folder to hold only AfterPub.exe, but it holds: $names"
    }

    [string]$productVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
    if (-not $productVersion.StartsWith($version))
    {
        throw "The exe reports version '$productVersion', which does not start with '$version'."
    }

    $sizeMb = [math]::Round($items[0].Length / 1MB, 1)
    Write-Host "AfterPub.exe is $sizeMb MB, version $productVersion."

    # --- Package -------------------------------------------------------------------------
    Write-Step 'Packaging'
    $zipName = "AfterPub-$version-win-x64.zip"
    $zipPath = Join-Path $artifacts $zipName
    if (Test-Path -LiteralPath $zipPath)
    {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -LiteralPath $exe -DestinationPath $zipPath
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

    $shaPath = "$zipPath.sha256"
    Write-Utf8File $shaPath "$hash  $zipName`n"

    if ([string]::IsNullOrEmpty($NotesFile))
    {
        $preLabel = ''
        if ($isPreRelease)
        {
            $preLabel = ' (release candidate)'
        }

        $notesText = (@(
            "AfterPub $version$preLabel",
            '',
            'Portable build for Windows 10 and 11 (64-bit). Unzip it and run AfterPub.exe. There is no installer, and .NET does not need to be installed.',
            '',
            'Windows may show a SmartScreen warning because the program is not code-signed yet. Choose More info, then Run anyway.',
            '',
            "SHA-256 of ${zipName}:",
            '',
            "    $hash"
        ) -join "`n") + "`n"
    }
    else
    {
        $notesText = Get-Content -LiteralPath $NotesFile -Raw
    }

    $notesPath = Join-Path $artifacts "release-notes-$tagName.md"
    Write-Utf8File $notesPath $notesText

    Write-Host ""
    Write-Host "Package:   $zipPath"
    Write-Host "Checksum:  $hash"
    Write-Host "Notes:     $notesPath"

    if (-not $doTag)
    {
        Write-Host ""
        Write-Host 'Built and packaged only; nothing was tagged or pushed.' -ForegroundColor Green
        Write-Host "Test the exe at $exe, then run this script again with -Publish (or -Tag)."
        return
    }

    # --- Tag, push, release --------------------------------------------------------------
    Write-Host ""
    Write-Host "Last chance to test the exact exe that will be released: $exe" -ForegroundColor Yellow
    if (-not $Yes)
    {
        $answer = Read-Host "Tag $tagName at commit $commit and push it to origin? Type yes to continue"
        if ($answer -ne 'yes')
        {
            Write-Host 'Stopped before tagging. Nothing was pushed.'
            return
        }
    }

    Invoke-Native "Creating tag $tagName" 'git' @('tag', '-a', $tagName, '-m', "AfterPub $version")
    Invoke-Native "Pushing tag $tagName" 'git' @('push', 'origin', $tagName)

    if ($Publish)
    {
        $releaseArgs = @(
            'release', 'create', $tagName, $zipPath, $shaPath,
            '--title', "AfterPub $version",
            '--notes-file', $notesPath,
            '--verify-tag')
        if ($isPreRelease)
        {
            $releaseArgs += '--prerelease'
        }

        Invoke-Native 'Creating the GitHub release' 'gh' $releaseArgs
    }

    Write-Host ""
    Write-Host "Done: $tagName." -ForegroundColor Green
}
catch
{
    Write-Host ""
    Write-Host "RELEASE STOPPED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally
{
    Pop-Location
}
