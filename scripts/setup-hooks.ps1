#!/usr/bin/env pwsh
<#
.SYNOPSIS
    One-time setup for this repo's git hooks. Run once after cloning.

.DESCRIPTION
    Wires `git config core.hooksPath .githooks` so the shell pre-commit hook
    runs on every commit. Then runs `dotnet tool restore` to install the
    JetBrains InspectCode CLI (`jb`) at the version pinned in
    `.config/dotnet-tools.json`. The tool is invoked locally as `dotnet jb`,
    not as a global tool, so every developer + CI run the same version.

    Idempotent — safe to re-run.

    Bypass an individual commit with: git commit --no-verify
#>
[CmdletBinding()]
param(
    [Alias('SkipJbInstall')]
    [switch] $SkipJbRestore
)

$ErrorActionPreference = 'Stop'

$repoRootRaw = & git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRootRaw)) {
    Write-Error "setup-hooks: not inside a git repo."
    exit 1
}
$repoRoot = $repoRootRaw.Trim()
Set-Location $repoRoot

# 1. core.hooksPath -> .githooks
$current = (& git config --local --get core.hooksPath 2>$null)
if ($current -ne '.githooks') {
    & git config --local core.hooksPath .githooks
    Write-Host "✓ git config core.hooksPath = .githooks" -ForegroundColor Green
} else {
    Write-Host "✓ git config core.hooksPath already set to .githooks" -ForegroundColor DarkGray
}

# 2. shell hook executable bit (no-op on Windows / NTFS)
$hook = Join-Path $repoRoot '.githooks/pre-commit'
if ((Test-Path $hook) -and (-not $IsWindows)) {
    & chmod +x $hook 2>$null
}

# 3. JetBrains.ReSharper.GlobalTools (`jb`) — pinned via .config/dotnet-tools.json
#    so every developer + CI runs the exact same InspectCode version.
#    `dotnet tool restore` is idempotent: it re-uses the existing install when
#    the manifest version is already present.
if ($SkipJbRestore) {
    Write-Host "↷ skipping jb restore (-SkipJbRestore)" -ForegroundColor DarkGray
} elseif (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Warning "dotnet not found on PATH. Install the .NET SDK then re-run this script."
} else {
    Write-Host "↻ dotnet tool restore (pinned via .config/dotnet-tools.json)..." -ForegroundColor Yellow
    & dotnet tool restore 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet tool restore failed. Run manually: dotnet tool restore"
    } else {
        $manifestVersion = $null
        $manifestPath = Join-Path $repoRoot '.config/dotnet-tools.json'
        if (Test-Path $manifestPath) {
            try {
                $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
                $manifestVersion = $manifest.tools.'jetbrains.resharper.globaltools'.version
            } catch { }
        }
        Write-Host "✓ jb restored$([string]::IsNullOrEmpty($manifestVersion) ? '' : " (version $manifestVersion)"); invoke as 'dotnet jb' or 'dotnet tool run jb'." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Hooks ready. Bypass any single commit with: git commit --no-verify" -ForegroundColor Cyan
