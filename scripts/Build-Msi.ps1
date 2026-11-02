param([string]$BinaryDirectory=(Join-Path (Split-Path -Parent $PSScriptRoot) 'dist'),[string]$OutputDirectory=(Join-Path (Split-Path -Parent $PSScriptRoot) 'releases'))
$repo=Split-Path -Parent $PSScriptRoot
$ErrorActionPreference='Stop'
if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)){throw 'Building the MSI needs the .NET 8 SDK. End users do not need this SDK.'}
$tools=Join-Path $repo 'dist\wix-tools'
$wix=Join-Path $tools 'wix.exe'
if(-not (Test-Path -LiteralPath $wix)){
    dotnet tool install wix --version 4.0.6 --tool-path $tools --add-source https://api.nuget.org/v3/index.json
    if($LASTEXITCODE -ne 0){throw 'WiX installation failed.'}
}
$env:DOTNET_ROLL_FORWARD='Major'
Push-Location $repo
try{
    foreach($extension in 'WixToolset.UI.wixext','WixToolset.Netfx.wixext'){
        & $wix extension add "$extension/4.0.6"
        if($LASTEXITCODE -ne 0){throw 'WiX extension installation failed.'}
    }
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $output=Join-Path $OutputDirectory 'ResMode-0.2.2-alpha.21-win-x64.msi'
    & $wix build (Join-Path $repo 'installer\ResMode.wxs') -arch x64 -culture pt-BR `
        -ext WixToolset.UI.wixext -ext WixToolset.Netfx.wixext `
        -d "BinaryDir=$([IO.Path]::GetFullPath($BinaryDirectory))" -d "SourceDir=$repo" -out $output
    if($LASTEXITCODE -ne 0){throw 'MSI build failed.'}
    Write-Output "Built $output"
}finally{Pop-Location}
