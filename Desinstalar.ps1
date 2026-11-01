# Remove o ResMode: encerra o app (voltando a resolução do desktop), apaga atalhos e a pasta de instalação.
$ErrorActionPreference = 'Stop'
$name = 'ResMode'
$installRoot = Join-Path $env:LOCALAPPDATA $name
$exe = Join-Path $installRoot "$name.exe"

if (Test-Path $exe) {
    # Volta para a resolução do desktop caso um jogo esteja aberto, antes de encerrar.
    Start-Process -FilePath $exe -ArgumentList '/restore' -Wait
    Get-Process -Name $name -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object { $_ | Stop-Process -Force; $_.WaitForExit(5000) | Out-Null }
}
foreach ($folder in [Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Startup')) {
    Remove-Item (Join-Path $folder "$name.lnk") -ErrorAction SilentlyContinue
}
if (Test-Path $installRoot) { Remove-Item $installRoot -Recurse -Force }
Write-Output 'ResMode removido.'
