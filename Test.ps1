param([string]$BinaryDirectory = (Join-Path $PSScriptRoot 'dist'), [switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
$binary = Join-Path $BinaryDirectory 'ResMode.exe'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build ResMode first with Build.ps1.' }
$reports = Join-Path $BinaryDirectory 'test-results'
New-Item -ItemType Directory -Force -Path $reports | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture = Join-Path $BinaryDirectory 'FeatureChecks.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 "/out:$fixture" "/reference:$binary" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll `
    (Join-Path $PSScriptRoot 'tests\FeatureChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression fixture compilation failed.' }
if ($CompileOnly) { Write-Output 'Fixture compiled; execution deliberately skipped.'; return }
foreach ($test in '/selftest','/preferencestest','features') {
    $report = Join-Path $reports ($test.TrimStart('/') + '-' + [guid]::NewGuid().ToString('N') + '.txt')
    $target = if ($test -eq 'features') { $fixture } else { $binary }
    $arguments = if ($test -eq 'features') { '"' + $report + '"' } else { $test + ' "' + $report + '"' }
    $process = Start-Process -FilePath $target -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "Test timeout: $test" }
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) {
        if (Test-Path -LiteralPath ($report + '.error')) { Get-Content -LiteralPath ($report + '.error') }
        throw "Test failed: $test (exit $($process.ExitCode))."
    }
    Get-Content -LiteralPath $report
}
