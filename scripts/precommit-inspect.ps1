#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pre-commit quality gate: runs JetBrains InspectCode on staged .cs files.

.DESCRIPTION
    Fails the commit when any warning-level Rider/Roslyn inspection fires in
    a staged file. Note-level suggestions are ignored.

    Tool resolution (in order):
      1. If `.config/dotnet-tools.json` exists and `dotnet` is on PATH, the
         script runs `dotnet tool restore` and invokes `dotnet jb` — i.e.
         the manifest-pinned local tool. This is the preferred path in
         this repo and keeps every developer + CI on the same version.
      2. Otherwise, falls back to a globally-installed `jb` (legacy).
      3. If neither is available and `dotnet` is on PATH, performs a one-
         time `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
         (only when no manifest is present). Pass `-SkipAutoInstall` to
         disable that fallback install. `-SkipAutoInstall` does NOT skip
         the manifest-based `dotnet tool restore` — that path always
         runs when a manifest is present.

    Bypass in an emergency with: git commit --no-verify

    One-time setup per machine (after cloning a repo that ships this script):
        pwsh scripts/setup-hooks.ps1
#>
[CmdletBinding()]
param(
    [string]   $Solution,
    [string[]] $IgnoredRules    = @(),
    [switch]   $SkipAutoInstall
)

$ErrorActionPreference = 'Stop'

$repoRootRaw = & git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRootRaw)) {
    Write-Warning "pre-commit: not inside a git repo; skipping inspection."
    exit 0
}
$repoRoot = $repoRootRaw.Trim()
Set-Location $repoRoot

if (-not $Solution) {
    # Auto-detect: prefer src/*.sln (the canonical layout in this org); only
    # fall back to a root-level *.sln when src/ has none. Using a flat
    # sort+first across both folders would let a stray root .sln win even
    # when src/*.sln exists, which is the opposite of what we want.
    $srcCandidates  = @(Get-ChildItem -Path 'src' -Filter '*.sln' -File -ErrorAction SilentlyContinue)
    $rootCandidates = @(Get-ChildItem -Path '.'   -Filter '*.sln' -File -ErrorAction SilentlyContinue)
    if ($srcCandidates.Count -gt 0) {
        $candidates = @($srcCandidates | Sort-Object FullName)
    } else {
        $candidates = @($rootCandidates | Sort-Object FullName)
    }
    if ($candidates.Count -eq 0) {
        Write-Warning "pre-commit: no .sln found under src/ or repo root; skipping inspection."
        exit 0
    }
    if ($candidates.Count -gt 1) {
        Write-Warning "pre-commit: multiple .sln files found, using $($candidates[0].FullName)."
        Write-Warning "  Pass -Solution <path> explicitly to override."
    }
    $Solution = (Resolve-Path $candidates[0].FullName -Relative)
}

$stagedFiles = & git diff --cached --name-only --diff-filter=ACMR | Where-Object { $_ -like '*.cs' }

if (-not $stagedFiles) {
    Write-Host "pre-commit: no staged .cs files - skipping inspection." -ForegroundColor DarkGray
    exit 0
}

# Use case-sensitive comparisons on POSIX filesystems (Linux/macOS) where
# Foo.cs and foo.cs are distinct files. Windows/NTFS is case-insensitive.
$pathComparer = if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }

function Ensure-Jb {
    # Prefer the manifest-pinned local tool (`dotnet jb`) over a globally-
    # installed `jb`, so the gate runs the exact version recorded in
    # .config/dotnet-tools.json. Returns a hashtable describing how to invoke
    # the tool: @{ Command = 'dotnet'|'jb'; Prefix = @('jb') | @() }.
    $manifest = Join-Path $repoRoot '.config/dotnet-tools.json'
    if ((Test-Path $manifest) -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        if (-not $script:JbRestored) {
            & dotnet tool restore 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "pre-commit: 'dotnet tool restore' failed; falling back to global jb if present."
            } else {
                $script:JbRestored = $true
            }
        }
        if ($script:JbRestored) {
            return @{ Command = 'dotnet'; Prefix = @('jb') }
        }
    }

    # Fallback: globally installed `jb` (legacy / user choice).
    if (Get-Command jb -ErrorAction SilentlyContinue) {
        return @{ Command = 'jb'; Prefix = @() }
    }
    $toolsDir = if ($IsWindows) {
        Join-Path $env:USERPROFILE '.dotnet\tools'
    } else {
        Join-Path $HOME '.dotnet/tools'
    }
    $jbBin = Join-Path $toolsDir (if ($IsWindows) { 'jb.exe' } else { 'jb' })
    if (Test-Path $jbBin) {
        $env:PATH = "$toolsDir$([IO.Path]::PathSeparator)$env:PATH"
        if (Get-Command jb -ErrorAction SilentlyContinue) {
            return @{ Command = 'jb'; Prefix = @() }
        }
    }
    return $null
}

$jb = Ensure-Jb
if ($null -eq $jb) {
    if ($SkipAutoInstall) {
        Write-Warning "pre-commit: 'jb' not available and -SkipAutoInstall set; skipping inspection."
        exit 0
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Warning "pre-commit: neither 'jb' nor 'dotnet' on PATH; skipping inspection."
        Write-Warning "Install the .NET SDK and run 'dotnet tool restore' from the repo root to enable."
        exit 0
    }

    # If a manifest exists, the pinned tool is the source of truth. Falling
    # back to a global install would silently swap the developer onto a
    # different (likely newer) InspectCode build than CI, defeating the
    # whole point of pinning. Fail-soft (skip) and surface the restore
    # error so the developer can fix it deliberately.
    $manifest = Join-Path $repoRoot '.config/dotnet-tools.json'
    if (Test-Path $manifest) {
        Write-Warning "pre-commit: tool manifest exists at .config/dotnet-tools.json but 'dotnet tool restore' failed."
        Write-Warning "  Not falling back to a global install — that would diverge from the pinned version used by CI."
        Write-Warning "  Run manually to diagnose: dotnet tool restore"
        Write-Warning "  Bypass in an emergency with: git commit --no-verify"
        exit 0
    }

    # No manifest + dotnet present — last-resort one-time global install.
    # The manifest path above is the preferred path; this branch only fires
    # in repos that haven't (yet) added .config/dotnet-tools.json.
    Write-Host "pre-commit: 'jb' not found and no tool manifest; installing JetBrains.ReSharper.GlobalTools (one-time)..." -ForegroundColor Yellow
    & dotnet tool install -g JetBrains.ReSharper.GlobalTools 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "pre-commit: auto-install failed; skipping inspection."
        Write-Warning "Run manually: dotnet tool install -g JetBrains.ReSharper.GlobalTools"
        exit 0
    }
    $jb = Ensure-Jb
    if ($null -eq $jb) {
        Write-Warning "pre-commit: 'jb' still not available after install; skipping inspection."
        Write-Warning "Add `$HOME/.dotnet/tools (or %USERPROFILE%\.dotnet\tools) to PATH and retry."
        exit 0
    }
    Write-Host "pre-commit: jb installed." -ForegroundColor Green
}

Write-Host "pre-commit: inspecting $($stagedFiles.Count) staged .cs file(s)..." -ForegroundColor Cyan

# The hook must inspect the exact content being committed (the index), not
# the working tree. If a developer has unstaged edits in a staged file, a
# working-tree inspection would either fail the commit on code that is not
# being committed, or — worse — pass after an unstaged fix while committing
# the still-broken staged version. Stash unstaged changes (and untracked
# files) with --keep-index so the working tree matches the index for the
# duration of the inspection, then restore afterward in `finally`.
#
# Detect a no-op stash by comparing refs/stash before and after: an actual
# stash entry advances that ref, while "nothing to stash" leaves it (or its
# absence) untouched. Without this we would `stash pop` someone else's
# previously-stashed work and corrupt their tree.
$preStashSha = (& git rev-parse --verify --quiet refs/stash 2>$null)
& git stash push --keep-index --include-untracked --quiet -m 'precommit-inspect: working-tree snapshot' 2>$null | Out-Null
$postStashSha = (& git rev-parse --verify --quiet refs/stash 2>$null)
$stashed = ($postStashSha -and ($postStashSha -ne $preStashSha))

$reportPath = Join-Path ([IO.Path]::GetTempPath()) "precommit-inspect-$([Guid]::NewGuid()).sarif"
# Mirror to $script: so Remove-Report (which references $script:reportPath) can
# actually find and delete the temp SARIF file on every exit path. Without this
# the function's $script:reportPath would be $null and the temp file would leak.
$script:reportPath = $reportPath

# Helper invoked before every `exit` below — PowerShell `exit` skips `finally`,
# so we explicitly run cleanup before each terminal path. Restores unstaged
# edits + untracked files that we stashed above so the developer's working
# tree survives the inspection (success or failure) intact.
function Restore-WorkingTree {
    if ($script:stashed) {
        & git stash pop --quiet 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "pre-commit: 'git stash pop' failed — your unstaged changes are still in the stash."
            Write-Warning "  Recover with: git stash list ; git stash pop"
        }
        $script:stashed = $false
    }
}
$script:stashed = $stashed

# --include uses file-name wildcards; we use **/<leaf> so patterns match the solution layout.
$includeMask = ($stagedFiles | ForEach-Object { "**/" + (Split-Path $_ -Leaf) } | Sort-Object -Unique) -join ';'

$jbArgs = @(
    'inspectcode',
    $Solution,
    "--output=$reportPath",
    "--include=$includeMask",
    '--no-build',
    '--verbosity=WARN'
)

$sw = [Diagnostics.Stopwatch]::StartNew()
$invokeArgs = $jb.Prefix + $jbArgs
& $jb.Command @invokeArgs | Out-Null
$jbExit = $LASTEXITCODE
$sw.Stop()
Write-Host "pre-commit: inspection finished in $([int]$sw.Elapsed.TotalSeconds)s." -ForegroundColor DarkGray

# Helper so the temp SARIF file is removed on every exit path — success,
# parse error, jb failure, or missing report. PowerShell `exit` skips
# enclosing `finally` blocks, so we explicitly cleanup before each `exit`.
# Also restores any working-tree stash we created earlier, since every
# `Remove-Report` site is downstream of the stash point.
function Remove-Report {
    if ($script:reportPath -and (Test-Path $script:reportPath)) {
        Remove-Item $script:reportPath -Force -ErrorAction SilentlyContinue
    }
    Restore-WorkingTree
}

# Build a copy/paste-safe rerun command. Quote any arg that contains whitespace
# or shell metacharacters so the temp SARIF path (and any other surprising
# value) round-trips correctly.
function Format-ShellArg {
    param([Parameter(Mandatory)][string]$Value)
    # Quote any value containing whitespace or shell metacharacters so the
    # temp SARIF path (and other surprising values) round-trip correctly.
    # Build the metachar list in a char-class via [regex] to avoid quote-
    # escaping headaches inside a PowerShell string literal.
    $needsQuoting = [regex]::IsMatch($Value, '[\s"\\$`!()*?\[\]{}|<>;&#~'']')
    if (-not $needsQuoting) { return $Value }
    # Prefer single quotes; if the value contains one, fall back to double
    # quoting with backtick-escaped specials.
    if ($Value -notmatch "'") { return "'" + $Value + "'" }
    return '"' + ($Value -replace '([\\"`$])','`$1') + '"'
}
$rerunParts = @($jb.Command) + $jb.Prefix + $jbArgs
$rerunCmd = ($rerunParts | ForEach-Object { Format-ShellArg $_ }) -join ' '

if ($jbExit -ne 0) {
    Remove-Report
    Write-Host "" -ForegroundColor Red
    Write-Host "pre-commit: '$($jb.Command) $($jb.Prefix -join ' ') inspectcode' exited with code $jbExit (restore/build/tool failure)." -ForegroundColor Red
    Write-Host "  Re-run manually to see the full output:" -ForegroundColor Yellow
    Write-Host "    $rerunCmd" -ForegroundColor Yellow
    Write-Host "  Bypass with: git commit --no-verify" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $reportPath)) {
    # No report produced — nothing to clean up, but call Remove-Report for
    # symmetry in case a partial file exists.
    Remove-Report
    Write-Host "pre-commit: 'jb inspectcode' returned 0 but produced no report at $reportPath." -ForegroundColor Red
    Write-Host "  This usually means the include mask matched no files in the solution." -ForegroundColor Yellow
    Write-Host "  Bypass with: git commit --no-verify" -ForegroundColor Yellow
    exit 1
}

$stagedSet = [System.Collections.Generic.HashSet[string]]::new($pathComparer)
$stagedFiles | ForEach-Object { [void]$stagedSet.Add(($_ -replace '\\', '/')) }

try {
    $sarif = Get-Content $reportPath -Raw | ConvertFrom-Json
} catch {
    Remove-Report
    Write-Host "pre-commit: failed to parse SARIF report at ${reportPath}: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Remove-Report

# Normalize IgnoredRules: trim, drop empties, and split on commas in case a
# single comma-separated string was passed (e.g., `-IgnoredRules "IDE0005, CA1822"`).
# Without this, callers passing a CSV value would never match because the leading
# space (or the bare comma) would prevent equality with SARIF rule IDs.
$ignoredRulesNormalized = @(
    $IgnoredRules |
        Where-Object { $_ -ne $null } |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() } |
        Where-Object   { $_ -ne '' }
)

$results = @($sarif.runs[0].results | Where-Object {
        $_.level -eq 'warning' -and
        $_.ruleId -notin $ignoredRulesNormalized
    })

# SARIF artifactLocation.uri can be:
#   - repo-relative POSIX path (the common case from `jb inspectcode`)
#   - absolute path (Windows or POSIX)
#   - file:// or file:/// URI
#   - './'-prefixed repo-relative path
# Normalize all of these to repo-relative POSIX before set membership, otherwise
# real violations in staged files would silently fall through and the gate would
# pass commits it should block.
$repoRootPosix = ($repoRoot -replace '\\', '/').TrimEnd('/')
function Normalize-SarifUri {
    param([Parameter(Mandatory)][string]$Uri)
    $u = $Uri
    # file:// URIs need [System.Uri] parsing to handle Windows-form
    # `file:///C:/repo/...` correctly. A naive `^file:/{2,3}` -> '/'
    # replace would yield '/C:/repo/...' which would never match
    # $repoRootPosix on Windows.
    if ($u -match '^file:') {
        try {
            $u = ([System.Uri]$u).LocalPath
        } catch {
            $u = $u -replace '^file:/{2,3}', ''
        }
    }
    $u = $u -replace '\\', '/'
    if ($u -like './*') { $u = $u.Substring(2) }
    # Absolute path inside the repo root → strip prefix (case-insensitive on Windows).
    $cmp = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if ($u.StartsWith("$repoRootPosix/", $cmp)) { $u = $u.Substring($repoRootPosix.Length + 1) }
    return $u.TrimStart('/')
}

# Only fail for issues in files that were actually staged (filename-wildcard
# include may match same-named files in other folders).
$failures = @($results | Where-Object {
        $uri = Normalize-SarifUri $_.locations[0].physicalLocation.artifactLocation.uri
        $stagedSet.Contains($uri)
    })

if ($failures.Count -eq 0) {
    Restore-WorkingTree
    Write-Host "pre-commit: no blocking issues in staged files. ✔" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "pre-commit: $($failures.Count) blocking issue(s) in staged files:" -ForegroundColor Red
Write-Host ""

$failures | Sort-Object { $_.locations[0].physicalLocation.artifactLocation.uri } | ForEach-Object {
    $loc = $_.locations[0].physicalLocation
    $file = $loc.artifactLocation.uri
    $line = $loc.region.startLine
    "  {0}:{1}  [{2}]  {3}" -f $file, $line, $_.ruleId, $_.message.text
} | Write-Host

Write-Host ""
Write-Host "Fix the issues above and re-stage, or bypass with: git commit --no-verify" -ForegroundColor Yellow
Restore-WorkingTree
exit 1
