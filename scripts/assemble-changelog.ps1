#Requires -Version 5.1

<#
.SYNOPSIS
    Folds the changelog fragments in changelog.d/ into CHANGELOG.md under a version heading.

.DESCRIPTION
    Every change lands on its branch as its own file in changelog.d/ rather than as an edit to
    CHANGELOG.md, because a shared file edited by every branch is a guaranteed merge conflict and
    a new file is never one. This script is the other half of that trade: at release time it
    collects the fragments, groups their bullets by category, writes a single version section
    into CHANGELOG.md, and deletes the fragments it consumed.

    A fragment is:

        category: Added
        - What changed, written for someone embedding the package.

    Line 1 is the category; the remaining non-blank lines are markdown bullets, copied verbatim.
    Continuation lines of a wrapped bullet are preserved as written. changelog.d/README.md is
    never treated as a fragment, so the directory survives the deletion step.

    Adapted from Munin's scripts/assemble-changelog.ps1, with the bilingual half removed: this
    package is consumed by developers and its changelog is English only. See changelog.d/README.md.

    Unlike Munin's version this writes a released version section rather than merging into an
    [Unreleased] section. CHANGELOG.md here is the released record; anything not yet released is
    a fragment. That keeps CHANGELOG.md untouched between releases, which is the whole point.

    Running it twice for one version is a no-op, not an error. .github/workflows/release.yml runs
    it on every run of a tag, re-runs included, and a re-run has to reach the pack step: a second
    call finds the section already there, writes no duplicate, consumes no fragment — so the
    fragments queued for the NEXT release survive it — and still answers -NotesOutFile.

.PARAMETER Version
    Version for the new section, e.g. "0.2.0". Defaults to VersionPrefix in Directory.Build.props.

.PARAMETER Date
    Release date as yyyy-MM-dd. Defaults to today.

.PARAMETER NotesOutFile
    Write this version's section body — the bullets, without the version heading — to this file.
    The release workflow stamps it into PackageReleaseNotes and into the GitHub release, so a
    consumer reads the entry itself rather than a link to go and find one. Written on every exit
    that has an answer: the section just assembled, or the one already in CHANGELOG.md; empty when
    there is nothing to release.

.PARAMETER DryRun
    Print the section that would be written and leave every file alone.

.EXAMPLE
    ./scripts/assemble-changelog.ps1 -DryRun

.EXAMPLE
    ./scripts/assemble-changelog.ps1 -Version 0.2.0

.NOTES
    Run from anywhere; paths are resolved relative to the script.
#>

param(
    [string]$Version,
    [string]$Date,
    [string]$NotesOutFile,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Success { param($Message) Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Info    { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Err     { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

# Section order in the assembled output. "Notes for hosts" is last on purpose: it is standing
# guidance about mounting the component rather than a change, so it reads as a footnote to the
# release. The other six are the Keep a Changelog set.
$CategoryOrder = @('Added', 'Changed', 'Fixed', 'Security', 'Deprecated', 'Removed', 'Notes for hosts')

# The assembly script writes new version sections directly below this marker, newest first.
$InsertMarker = '<!-- assemble-changelog:'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$FragmentDir = Join-Path $RepoRoot 'changelog.d'
$ChangelogPath = Join-Path $RepoRoot 'CHANGELOG.md'

# --- Version -------------------------------------------------------------------------------
if (-not $Version) {
    $propsPath = Join-Path $RepoRoot 'Directory.Build.props'
    if (Test-Path $propsPath) {
        $propsText = Get-Content $propsPath -Raw -Encoding UTF8
        if ($propsText -match '<VersionPrefix>\s*([^<]+?)\s*</VersionPrefix>') {
            $Version = $Matches[1]
            Write-Info "No -Version given; using VersionPrefix from Directory.Build.props: $Version"
        }
    }
}
if (-not $Version) {
    Write-Err 'No -Version given and VersionPrefix could not be read from Directory.Build.props.'
    exit 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$') {
    Write-Err "Version '$Version' is not semver (expected 1.2.3 or 1.2.3-preview.1)."
    exit 1
}

if (-not $Date) { $Date = (Get-Date).ToString('yyyy-MM-dd') }
if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') {
    Write-Err "Date '$Date' is not yyyy-MM-dd."
    exit 1
}

# --- Notes -----------------------------------------------------------------------------------
# Returns the body of an existing version's section — every line under its heading, up to the
# next one — or $null when the version has no section yet. A string rather than an array so the
# caller can tell "no section" from "a section with nothing in it": an empty array compared to
# $null answers false in PowerShell, which would read as absent.
function Get-SectionBody {
    param([string[]]$Lines, [string]$Wanted)

    $heading = '^##\s+' + [regex]::Escape($Wanted) + '(\s|$)'
    $body = [System.Collections.Generic.List[string]]::new()
    $inside = $false
    foreach ($line in $Lines) {
        if ($inside) {
            if ($line -match '^##\s') { break }
            $body.Add($line)
        }
        elseif ($line -match $heading) { $inside = $true }
    }

    if (-not $inside) { return $null }
    return ($body -join "`n")
}

function Write-Notes {
    param([string]$Body)

    $text = $Body.Trim()
    if ($text -ne '') { $text += "`n" }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($NotesOutFile, $text, $utf8NoBom)
}

if (-not (Test-Path $ChangelogPath)) {
    Write-Err "CHANGELOG.md not found at $ChangelogPath"
    exit 1
}
$changelogLines = @(Get-Content $ChangelogPath -Encoding UTF8)

if ($NotesOutFile -and -not [System.IO.Path]::IsPathRooted($NotesOutFile)) {
    $NotesOutFile = Join-Path (Get-Location).Path $NotesOutFile
}

# Already assembled: a no-op, and deliberately not an error. See the header — a re-run of a
# release has to get past this line, and the fragments waiting for the next release have to
# survive it.
$existingBody = Get-SectionBody $changelogLines $Version
if ($null -ne $existingBody) {
    Write-Info "CHANGELOG.md already has a section for $Version — nothing to assemble."
    if ($NotesOutFile) {
        Write-Notes $existingBody
        Write-Info "Wrote the section already in CHANGELOG.md to $NotesOutFile"
    }
    exit 0
}

# --- Read fragments ------------------------------------------------------------------------
if (-not (Test-Path $FragmentDir)) {
    Write-Err "changelog.d/ not found at $FragmentDir"
    exit 1
}

$fragments = @(Get-ChildItem -Path $FragmentDir -Filter '*.md' -File |
    Where-Object { $_.Name -ne 'README.md' } |
    Sort-Object Name)

if ($fragments.Count -eq 0) {
    Write-Info 'No fragments in changelog.d/ — nothing to release.'
    # An empty notes file, not an absent one: the caller asked for an answer and "this version
    # has no entry" is one. release.yml tests the file's size to choose its fallback.
    if ($NotesOutFile) { Write-Notes '' }
    exit 0
}

# Fragments are validated up front and the run aborts on the first bad one rather than silently
# skipping it. A skipped fragment is a change that quietly never reaches the changelog, and the
# fragment file would be left behind to be assembled into the *next* release — worse than a stop.
$grouped = [ordered]@{}
$problems = [System.Collections.Generic.List[string]]::new()

foreach ($file in $fragments) {
    $lines = @(Get-Content $file.FullName -Encoding UTF8)

    if ($lines.Count -eq 0) {
        $problems.Add("$($file.Name): file is empty")
        continue
    }

    if ($lines[0].Trim() -notmatch '^category:\s*(.+)$') {
        $problems.Add("$($file.Name): first line must be 'category: <Category>', found '$($lines[0])'")
        continue
    }
    $category = $Matches[1].Trim()

    if ($CategoryOrder -notcontains $category) {
        $problems.Add("$($file.Name): unknown category '$category' — expected one of: $($CategoryOrder -join ', ')")
        continue
    }

    $body = @($lines | Select-Object -Skip 1 | Where-Object { $_.Trim() -ne '' })
    if ($body.Count -eq 0) {
        $problems.Add("$($file.Name): no bullet lines after the category header")
        continue
    }
    if ($body[0].TrimStart() -notmatch '^[-*]\s') {
        $problems.Add("$($file.Name): first entry line must be a markdown bullet ('- ...')")
        continue
    }

    if (-not $grouped.Contains($category)) { $grouped[$category] = @() }
    $grouped[$category] += $body
}

if ($problems.Count -gt 0) {
    Write-Err 'Invalid changelog fragment(s) — nothing was assembled:'
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    Write-Host '  See changelog.d/README.md for the format.' -ForegroundColor Red
    exit 1
}

# --- Build the section ---------------------------------------------------------------------
$section = [System.Collections.Generic.List[string]]::new()
$section.Add("## $Version — $Date")
foreach ($category in $CategoryOrder) {
    if (-not $grouped.Contains($category)) { continue }
    $section.Add('')
    $section.Add("### $category")
    $section.Add('')
    foreach ($line in $grouped[$category]) { $section.Add($line) }
}

$rendered = ($section -join "`n")

if ($DryRun) {
    Write-Info "Would consume $($fragments.Count) fragment(s) and write into CHANGELOG.md:"
    Write-Host ''
    Write-Host $rendered
    Write-Host ''
    if ($NotesOutFile) {
        Write-Notes (($section | Select-Object -Skip 1) -join "`n")
        Write-Info "Wrote the section that would be released to $NotesOutFile"
    }
    Write-Info 'Dry run — no files changed.'
    exit 0
}

# --- Write it ------------------------------------------------------------------------------
$changelog = $changelogLines

# Insert directly below the marker comment. Falling back to the first existing '## ' heading
# keeps the script working if the marker is ever edited away.
$insertAt = -1
for ($i = 0; $i -lt $changelog.Count; $i++) {
    if ($changelog[$i].StartsWith($InsertMarker)) { $insertAt = $i + 1; break }
}
if ($insertAt -lt 0) {
    for ($i = 0; $i -lt $changelog.Count; $i++) {
        if ($changelog[$i] -match '^## ') { $insertAt = $i; break }
    }
}
if ($insertAt -lt 0) { $insertAt = $changelog.Count }

$out = [System.Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $insertAt; $i++) { $out.Add($changelog[$i]) }
$out.Add('')
foreach ($line in $section) { $out.Add($line) }
for ($i = $insertAt; $i -lt $changelog.Count; $i++) { $out.Add($changelog[$i]) }

# Collapse any run of blank lines the insertion created into a single one.
$clean = [System.Collections.Generic.List[string]]::new()
foreach ($line in $out) {
    if ($line.Trim() -eq '' -and $clean.Count -gt 0 -and $clean[$clean.Count - 1].Trim() -eq '') { continue }
    $clean.Add($line)
}

# LF and no BOM: .gitattributes normalises the working tree to LF, and PowerShell 5.1's
# -Encoding UTF8 would write a BOM.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ChangelogPath, (($clean -join "`n").TrimEnd() + "`n"), $utf8NoBom)
Write-Success "Wrote '## $Version — $Date' to CHANGELOG.md"

if ($NotesOutFile) {
    Write-Notes (($section | Select-Object -Skip 1) -join "`n")
    Write-Success "Wrote the section for $Version to $NotesOutFile"
}

foreach ($file in $fragments) { Remove-Item $file.FullName -Force }
Write-Success "Deleted $($fragments.Count) consumed fragment(s) from changelog.d/"

Write-Info 'Review CHANGELOG.md, then commit it together with the deletions.'
