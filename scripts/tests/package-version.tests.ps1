$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptsDirectory = Split-Path $PSScriptRoot -Parent
$verifyScript = Join-Path $scriptsDirectory 'verify-package.ps1'
$artifactsDirectory = Join-Path (Split-Path $scriptsDirectory -Parent) 'artifacts'
$testDirectory = Join-Path $artifactsDirectory "package-version-tests-$([Guid]::NewGuid().ToString('N'))"

function New-TestPackage {
    param(
        [string] $Directory,
        [string] $Version,
        [string] $Id = 'JamesConsulting',
        [string] $FileName = "$Id.$Version.nupkg"
    )

    [void] [IO.Directory]::CreateDirectory($Directory)
    $archive = [IO.Compression.ZipFile]::Open(
        (Join-Path $Directory $FileName), [IO.Compression.ZipArchiveMode]::Create)
    try {
        $writer = [IO.StreamWriter]::new($archive.CreateEntry("$Id.nuspec").Open())
        try {
            $writer.Write(
                "<package xmlns=`"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd`"><metadata><id>$Id</id><version>$Version</version></metadata></package>")
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-Rejected {
    param([scriptblock] $Action, [string] $ExpectedMessage)

    $failure = $null
    try {
        & $Action
    }
    catch {
        $failure = $_
    }
    if ($null -eq $failure -or $failure.Exception.Message -notlike $ExpectedMessage) {
        throw "Expected rejection matching '$ExpectedMessage'; received '$failure'."
    }
}

[void] [IO.Directory]::CreateDirectory($testDirectory)
try {
    foreach ($version in @('3.0.0-alpha.1.g0123456789', '3.0.0-rc.1', '3.0.0')) {
        $directory = Join-Path $testDirectory $version
        New-TestPackage -Directory $directory -Version $version
        & $verifyScript -PackageDirectory $directory -ExpectedVersion $version
    }

    $directory = Join-Path $testDirectory 'wrong-version'
    New-TestPackage -Directory $directory -Version '2.0.0' -FileName 'JamesConsulting.3.0.0.nupkg'
    Assert-Rejected {
        & $verifyScript -PackageDirectory $directory -ExpectedVersion '3.0.0'
    } "Package version '2.0.0' does not match expected version '3.0.0'."

    $directory = Join-Path $testDirectory 'wrong-id'
    New-TestPackage -Directory $directory -Version '3.0.0' -Id 'Other' -FileName 'JamesConsulting.3.0.0.nupkg'
    Assert-Rejected {
        & $verifyScript -PackageDirectory $directory -ExpectedVersion '3.0.0'
    } "Package ID 'Other' does not match expected ID 'JamesConsulting'."

    $directory = Join-Path $testDirectory 'wrong-filename'
    New-TestPackage -Directory $directory -Version '3.0.0' -FileName 'JamesConsulting.2.0.0.nupkg'
    Assert-Rejected {
        & $verifyScript -PackageDirectory $directory -ExpectedVersion '3.0.0'
    } "Package filename 'JamesConsulting.2.0.0.nupkg' does not match expected filename 'JamesConsulting.3.0.0.nupkg'."

    $directory = Join-Path $testDirectory 'extra-package'
    New-TestPackage -Directory $directory -Version '3.0.0'
    New-TestPackage -Directory $directory -Version '2.0.0'
    Assert-Rejected {
        & $verifyScript -PackageDirectory $directory -ExpectedVersion '3.0.0'
    } 'Expected exactly one .nupkg file; found 2.'

    $directory = Join-Path $testDirectory 'no-package'
    [void] [IO.Directory]::CreateDirectory($directory)
    Assert-Rejected {
        & $verifyScript -PackageDirectory $directory -ExpectedVersion '3.0.0'
    } 'Expected exactly one .nupkg file; found 0.'

    Write-Output 'Package version checks passed (8 scenarios).'
}
finally {
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}
