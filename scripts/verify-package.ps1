<#
.SYNOPSIS
Rejects missing, stale, or mismatched NuGet artifacts before signing and publishing.
.PARAMETER PackageDirectory
Directory that must contain exactly one JamesConsulting package.
.PARAMETER ExpectedVersion
The NuGetPackageVersion calculated by NBGV for the release commit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $PackageDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File)
if ($packages.Count -ne 1) {
    throw "Expected exactly one .nupkg file; found $($packages.Count)."
}

$package = $packages[0]
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $manifests = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) })
    if ($manifests.Count -ne 1) {
        throw "Expected exactly one package manifest; found $($manifests.Count)."
    }

    $stream = $manifests[0].Open()
    try {
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try {
            $manifest = [Xml.XmlDocument]::new()
            $manifest.XmlResolver = $null
            $manifest.Load($reader)
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$id = $manifest.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="id"]')
$version = $manifest.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="version"]')
if ($null -eq $id -or $null -eq $version) {
    throw 'Package manifest must contain an ID and version.'
}
if ($id.InnerText -cne 'JamesConsulting') {
    throw "Package ID '$($id.InnerText)' does not match expected ID 'JamesConsulting'."
}
if ($version.InnerText -cne $ExpectedVersion) {
    throw "Package version '$($version.InnerText)' does not match expected version '$ExpectedVersion'."
}

$expectedFileName = "JamesConsulting.$ExpectedVersion.nupkg"
if ($package.Name -cne $expectedFileName) {
    throw "Package filename '$($package.Name)' does not match expected filename '$expectedFileName'."
}

Write-Output "Verified package: $expectedFileName"
