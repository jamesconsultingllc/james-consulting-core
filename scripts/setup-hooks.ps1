#!/usr/bin/env pwsh
<#
.SYNOPSIS
    One-time setup for this repo's git hooks. Run once after cloning.

.DESCRIPTION
    Wires `git config core.hooksPath .githooks` so the shell pre-commit hook
    runs on every commit. Also installs the JetBrains InspectCode CLI
    (`jb`) as a global dotnet tool if it's not already on PATH and `dotnet`
    is available.

    Idempotent — safe to re-run.

    Bypass an individual commit with: git commit --no-verify
#>
[CmdletBinding()]
param(
    [switch] $SkipJbInstall
)

$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $repoRoot) {
    Write-Error "setup-hooks: not inside a git repo."
    exit 1
}
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

# 3. JetBrains.ReSharper.GlobalTools (`jb`)
if ($SkipJbInstall) {
    Write-Host "↷ skipping jb install (-SkipJbInstall)" -ForegroundColor DarkGray
} elseif (Get-Command jb -ErrorAction SilentlyContinue) {
    Write-Host "✓ jb already on PATH" -ForegroundColor DarkGray
} elseif (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Warning "dotnet not found on PATH. Install the .NET SDK then re-run this script."
} else {
    Write-Host "↻ installing JetBrains.ReSharper.GlobalTools (one-time)..." -ForegroundColor Yellow
    & dotnet tool install -g JetBrains.ReSharper.GlobalTools 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "jb install failed. Run manually: dotnet tool install -g JetBrains.ReSharper.GlobalTools"
    } else {
        $toolsDir = if ($IsWindows) {
            Join-Path $env:USERPROFILE '.dotnet\tools'
        } else {
            Join-Path $HOME '.dotnet/tools'
        }
        Write-Host "✓ jb installed to $toolsDir" -ForegroundColor Green
        if (-not (Get-Command jb -ErrorAction SilentlyContinue)) {
            Write-Warning "Add $toolsDir to your PATH for jb to be discovered in new shells."
        }
    }
}

Write-Host ""
Write-Host "Hooks ready. Bypass any single commit with: git commit --no-verify" -ForegroundColor Cyan
