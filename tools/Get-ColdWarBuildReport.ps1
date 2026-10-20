#Requires -Version 5.1
<#
.SYNOPSIS
Collects executable metadata needed to investigate Game Pass compatibility.
.DESCRIPTION
Reads a selected executable on disk and writes a JSON report. Does not open a
game process, read process memory, change game files, or contact a service.
#>
[CmdletBinding()]
param(
    [string] $ExePath,
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    if ($env:OS -ne 'Windows_NT') {
        throw 'Pass -ExePath with the path to the game executable.'
    }
    Add-Type -AssemblyName System.Windows.Forms
    $picker = New-Object System.Windows.Forms.OpenFileDialog
    try {
        $picker.Title = 'Select the Cold War game executable in its installation folder'
        $picker.Filter = 'Windows executables (*.exe)|*.exe'
        if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            return
        }
        $ExePath = $picker.FileName
    }
    finally {
        $picker.Dispose()
    }
}

$item = Get-Item -LiteralPath $ExePath
if ($item -isnot [System.IO.FileInfo]) {
    throw 'Select an executable file, not a directory.'
}

$stream = [System.IO.File]::Open(
    $item.FullName,
    [System.IO.FileMode]::Open,
    [System.IO.FileAccess]::Read,
    [System.IO.FileShare]::Read
)
$reader = $null
$hasher = $null
try {
    $reader = [System.IO.BinaryReader]::new($stream)
    if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) {
        throw 'The selected file is not a Windows PE executable (missing DOS header).'
    }
    $stream.Position = 0x3C
    $peOffset = $reader.ReadUInt32()
    if ($peOffset -lt 64 -or [long]$peOffset + 26 -gt $stream.Length) {
        throw 'The selected executable has an invalid or truncated PE header.'
    }
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550) {
        throw 'The selected file is missing its PE signature.'
    }
    $machine = $reader.ReadUInt16()
    $sectionCount = $reader.ReadUInt16()
    $timestamp = $reader.ReadUInt32()
    $stream.Position = [long]$peOffset + 20
    $optionalHeaderSize = $reader.ReadUInt16()
    $null = $reader.ReadUInt16() # Characteristics
    if ($optionalHeaderSize -lt 2 -or
        [long]$peOffset + 24 + $optionalHeaderSize + 40L * $sectionCount -gt $stream.Length) {
        throw 'The selected executable has a truncated optional header or section table.'
    }
    $magic = $reader.ReadUInt16()
    if ($magic -notin @(0x10B, 0x20B)) {
        throw 'The selected executable has an unsupported optional header.'
    }
    $architecture = switch ($machine) {
        0x8664 { 'x64' }
        0x014C { 'x86' }
        0xAA64 { 'ARM64' }
        default { 'unknown' }
    }

    $sizeBytes = $stream.Length
    $stream.Position = 0
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    $sha256 = [System.BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName)
    $report = [ordered]@{
        schemaVersion = 1
        capturedAtUtc = [DateTime]::UtcNow.ToString('o')
        editionReportedByUser = 'PC Xbox / Game Pass'
        executable = [ordered]@{
            name = $item.Name
            sizeBytes = $sizeBytes
            sha256 = $sha256
            fileVersion = $versionInfo.FileVersion
            productVersion = $versionInfo.ProductVersion
            architecture = $architecture
            machineCode = ('0x{0:X4}' -f $machine)
            optionalHeaderMagic = ('0x{0:X4}' -f $magic)
            peTimeDateStamp = $timestamp
        }
    }
}
finally {
    if ($hasher) { $hasher.Dispose() }
    if ($reader) { $reader.Dispose() }
    $stream.Dispose()
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Get-Location).Path ('cold-war-build-{0}.json' -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$json = $report | ConvertTo-Json -Depth 5
$bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($json + [Environment]::NewLine)
$outputStream = [System.IO.File]::Open($outputFullPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
try {
    $outputStream.Write($bytes, 0, $bytes.Length)
}
finally {
    $outputStream.Dispose()
}
Write-Output ('Build report saved: {0}' -f $outputFullPath)
