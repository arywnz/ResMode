param(
    [string]$BinaryDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'releases\package'),
    [string]$ArchivePath = (Join-Path $PSScriptRoot 'releases\ResMode-0.2.2-alpha.7-win-x64-unsigned.zip')
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0) { throw 'Package directory must be empty; previous output preserved.' }
if (Test-Path -LiteralPath $ArchivePath) { throw 'Archive already exists; previous release preserved.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory,(Join-Path $OutputDirectory 'dist'),(Join-Path $OutputDirectory 'assets\fonts'),(Split-Path -Parent $ArchivePath) | Out-Null
foreach ($file in 'ResMode.exe','ResMode.ico') { Copy-Item -LiteralPath (Join-Path $BinaryDirectory $file) -Destination (Join-Path $OutputDirectory 'dist') }
foreach ($file in 'Instalar.cmd','Instalar.ps1','Desinstalar.cmd','Desinstalar.ps1','README.md','LICENSE','THIRD_PARTY_NOTICES.md','PRIVACY.md','CODE_SIGNING.md','SECURITY.md') { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $OutputDirectory }
Copy-Item -LiteralPath (Join-Path $root 'assets\ResMode-logo.png') -Destination (Join-Path $OutputDirectory 'assets')
Copy-Item -LiteralPath (Join-Path $root 'assets\fonts\OFL.txt') -Destination (Join-Path $OutputDirectory 'assets\fonts')
$base = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
$checksums = Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($base.Length).Replace('\','/')
}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
Compress-Archive -Path (Join-Path $OutputDirectory '*') -DestinationPath $ArchivePath -CompressionLevel Optimal
Write-Output "Packaged $ArchivePath"
