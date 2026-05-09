#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pre-commit quality gate: runs JetBrains InspectCode on staged .cs files.

.DESCRIPTION
    Fails the commit when any warning-level Rider/Roslyn inspection fires in
    a staged file. Note-level suggestions are ignored.

    First run: if `jb` is missing and `dotnet` is on PATH, this script will
    install JetBrains.ReSharper.GlobalTools as a global tool automatically.
    Pass `-SkipAutoInstall` to disable that behavior.

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

function Ensure-JbOnPath {
    if (Get-Command jb -ErrorAction SilentlyContinue) { return $true }

    # JetBrains.ReSharper.GlobalTools installs to the dotnet tools directory,
    # which is on PATH by default on most setups but not always in a fresh
    # shell. Probe the canonical location and prepend it for this run.
    $toolsDir = if ($IsWindows) {
        Join-Path $env:USERPROFILE '.dotnet\tools'
    } else {
        Join-Path $HOME '.dotnet/tools'
    }
    if (Test-Path (Join-Path $toolsDir (if ($IsWindows) { 'jb.exe' } else { 'jb' }))) {
        $env:PATH = "$toolsDir$([IO.Path]::PathSeparator)$env:PATH"
        if (Get-Command jb -ErrorAction SilentlyContinue) { return $true }
    }
    return $false
}

if (-not (Ensure-JbOnPath)) {
    if ($SkipAutoInstall) {
        Write-Warning "pre-commit: 'jb' not on PATH and -SkipAutoInstall set; skipping inspection."
        exit 0
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Warning "pre-commit: neither 'jb' nor 'dotnet' on PATH; skipping inspection."
        Write-Warning "Install .NET SDK + 'dotnet tool install -g JetBrains.ReSharper.GlobalTools' to enable."
        exit 0
    }

    Write-Host "pre-commit: 'jb' not found, installing JetBrains.ReSharper.GlobalTools (one-time)..." -ForegroundColor Yellow
    & dotnet tool install -g JetBrains.ReSharper.GlobalTools 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "pre-commit: auto-install failed; skipping inspection."
        Write-Warning "Run manually: dotnet tool install -g JetBrains.ReSharper.GlobalTools"
        exit 0
    }
    if (-not (Ensure-JbOnPath)) {
        Write-Warning "pre-commit: 'jb' still not on PATH after install; skipping inspection."
        Write-Warning "Add `$HOME/.dotnet/tools (or %USERPROFILE%\.dotnet\tools) to PATH and retry."
        exit 0
    }
    Write-Host "pre-commit: jb installed." -ForegroundColor Green
}

Write-Host "pre-commit: inspecting $($stagedFiles.Count) staged .cs file(s)..." -ForegroundColor Cyan

$reportPath = Join-Path ([IO.Path]::GetTempPath()) "precommit-inspect-$([Guid]::NewGuid()).sarif"

# --include uses file-name wildcards; we use **/<leaf> so patterns match the solution layout.
$includeMask = ($stagedFiles | ForEach-Object { "**/" + (Split-Path $_ -Leaf) } | Sort-Object -Unique) -join ';'

$jbArgs = @(
    $Solution,
    "--output=$reportPath",
    "--include=$includeMask",
    '--no-build',
    '--verbosity=WARN'
)

$sw = [Diagnostics.Stopwatch]::StartNew()
& jb inspectcode @jbArgs | Out-Null
$jbExit = $LASTEXITCODE
$sw.Stop()
Write-Host "pre-commit: inspection finished in $([int]$sw.Elapsed.TotalSeconds)s." -ForegroundColor DarkGray

if ($jbExit -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "pre-commit: 'jb inspectcode' exited with code $jbExit (restore/build/tool failure)." -ForegroundColor Red
    Write-Host "  Re-run manually to see the full output:" -ForegroundColor Yellow
    Write-Host "    jb inspectcode $($jbArgs -join ' ')" -ForegroundColor Yellow
    Write-Host "  Bypass with: git commit --no-verify" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $reportPath)) {
    Write-Host "pre-commit: 'jb inspectcode' returned 0 but produced no report at $reportPath." -ForegroundColor Red
    Write-Host "  This usually means the include mask matched no files in the solution." -ForegroundColor Yellow
    Write-Host "  Bypass with: git commit --no-verify" -ForegroundColor Yellow
    exit 1
}

$stagedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$stagedFiles | ForEach-Object { [void]$stagedSet.Add(($_ -replace '\\', '/')) }

$sarif = Get-Content $reportPath -Raw | ConvertFrom-Json
Remove-Item $reportPath -Force -ErrorAction SilentlyContinue

$results = @($sarif.runs[0].results | Where-Object {
        $_.level -eq 'warning' -and
        $_.ruleId -notin $IgnoredRules
    })

# Only fail for issues in files that were actually staged (filename-wildcard
# include may match same-named files in other folders).
$failures = @($results | Where-Object {
        $uri = $_.locations[0].physicalLocation.artifactLocation.uri -replace '\\', '/'
        $stagedSet.Contains($uri)
    })

if ($failures.Count -eq 0) {
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
exit 1
