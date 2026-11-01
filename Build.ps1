# Build only: never installs, starts the app, changes display mode or signs locally.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'ResMode requires Windows x64.' }
$root = $PSScriptRoot
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& (Join-Path $root 'Build-Icon.ps1') -OutputDirectory $OutputDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter *.cs | Sort-Object Name | ForEach-Object FullName)
$resources = @(Get-ChildItem -LiteralPath (Join-Path $root 'assets\fonts') -Filter *.ttf | Sort-Object Name | ForEach-Object { '/resource:' + $_.FullName + ',ResMode.Fonts.' + $_.Name })
if ($resources.Count -ne 8) { throw 'Expected all eight original Geist TTF files.' }
$resources += '/resource:' + (Join-Path $root 'assets\fonts\OFL.txt') + ',ResMode.Fonts.OFL.txt'
$exe = Join-Path $OutputDirectory 'ResMode.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$exe" "/win32icon:$(Join-Path $OutputDirectory 'ResMode.ico')" "/win32manifest:$(Join-Path $root 'app.manifest')" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Core.dll `
    /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll $resources $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($metadata.ProductName -ne 'ResMode' -or $metadata.FileVersion -ne '0.1.0.0') { throw 'Unexpected release metadata.' }
Write-Output "Built $exe"
