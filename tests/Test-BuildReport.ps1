#Requires -Version 5.1
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../tools/Get-ColdWarBuildReport.ps1'
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('cw-report-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDirectory

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Rejected([string] $InputFile, [string] $ReportPath) {
    $failed = $false
    try { & $scriptPath -ExePath $InputFile -OutputPath $ReportPath | Out-Null }
    catch { $failed = $true }
    Assert-True $failed 'Invalid input must be rejected.'
    Assert-True (-not (Test-Path -LiteralPath $ReportPath)) 'Rejected input must not produce a report.'
}

try {
    $fixture = Join-Path $testDirectory 'fixture.exe'
    $buffer = New-Object byte[] 512
    $memory = [System.IO.MemoryStream]::new($buffer)
    $writer = [System.IO.BinaryWriter]::new($memory)
    try {
        $writer.Write([UInt16]0x5A4D)
        $memory.Position = 0x3C
        $writer.Write([UInt32]0x40)
        $memory.Position = 0x40
        $writer.Write([UInt32]0x4550)
        $writer.Write([UInt16]0x8664)
        $writer.Write([UInt16]1)
        $writer.Write([UInt32]1234567890)
        $memory.Position = 0x54
        $writer.Write([UInt16]240)
        $writer.Write([UInt16]0x22)
        $writer.Write([UInt16]0x20B)
    }
    finally { $writer.Dispose(); $memory.Dispose() }
    [System.IO.File]::WriteAllBytes($fixture, $buffer)
    $initialHash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash
    $reportPath = Join-Path $testDirectory 'report.json'
    & $scriptPath -ExePath $fixture -OutputPath $reportPath | Out-Null
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    Assert-True ($report.schemaVersion -eq 1) 'Unexpected schema version.'
    Assert-True ($report.executable.sha256 -ceq $initialHash.ToLowerInvariant()) 'SHA-256 must identify the complete input file.'
    Assert-True ($report.executable.architecture -eq 'x64') 'PE machine must be decoded correctly.'
    Assert-True ($report.executable.peTimeDateStamp -eq 1234567890) 'COFF timestamp must be decoded correctly.'
    Assert-True ($report.executable.sizeBytes -eq 512) 'File length must be reported correctly.'
    Assert-True ($report.executable.name -eq 'fixture.exe') 'Only the executable basename should be reported.'
    Assert-True (-not (Get-Content -LiteralPath $reportPath -Raw).Contains($testDirectory)) 'Report must not disclose the install path.'
    Assert-True ((Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash -eq $initialHash) 'Input must remain unchanged.'

    $previousReport = [System.IO.File]::ReadAllBytes($reportPath)
    $collisionRejected = $false
    try { & $scriptPath -ExePath $fixture -OutputPath $reportPath | Out-Null }
    catch { $collisionRejected = $true }
    Assert-True $collisionRejected 'Existing output files must not be overwritten.'
    Assert-True ([Convert]::ToBase64String([System.IO.File]::ReadAllBytes($reportPath)) -eq [Convert]::ToBase64String($previousReport)) 'Existing output must remain unchanged.'

    $badFile = Join-Path $testDirectory 'invalid.exe'
    [System.IO.File]::WriteAllText($badFile, 'This is not an executable.')
    Assert-Rejected $badFile (Join-Path $testDirectory 'bad-report.json')

    $buffer[0x40] = 0
    [System.IO.File]::WriteAllBytes($badFile, $buffer)
    Assert-Rejected $badFile (Join-Path $testDirectory 'bad-signature-report.json')

    $buffer[0x40] = 0x50
    $buffer[0x3C] = 0xFF
    $buffer[0x3D] = 0xFF
    [System.IO.File]::WriteAllBytes($badFile, $buffer)
    Assert-Rejected $badFile (Join-Path $testDirectory 'truncated-report.json')

    Write-Output 'Build report tests passed.'
}
finally {
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}
