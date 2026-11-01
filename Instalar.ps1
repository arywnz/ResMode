# Compila, valida e instala o ResMode em %LOCALAPPDATA%\ResMode.
param([switch]$NoStart, [string]$PreviousInstallRoot)
$ErrorActionPreference = 'Stop'
$name = 'ResMode'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$installRoot = Join-Path $env:LOCALAPPDATA $name
if (-not $PreviousInstallRoot) {
    $candidates = @((Join-Path $env:LOCALAPPDATA 'SharpShift'))
    $packages = Join-Path $env:LOCALAPPDATA 'Packages'
    if (Test-Path -LiteralPath $packages) {
        $candidates += @(Get-ChildItem -LiteralPath $packages -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'LocalCache\Local\SharpShift' })
    }
    $PreviousInstallRoot = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'jogos.xml') } | Sort-Object { (Get-Item -LiteralPath (Join-Path $_ 'jogos.xml')).LastWriteTimeUtc } -Descending | Select-Object -First 1
}
New-Item -ItemType Directory -Force -Path $dist, $installRoot | Out-Null

$icon = Join-Path $dist "$name.ico"
$exe = Join-Path $dist "$name.exe"

# Release packages already contain the built executable; keep its signature intact.
# A source checkout has no dist binary and uses the same build script as CI.
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'Build.ps1') -OutputDirectory $dist
}
if (-not (Test-Path -LiteralPath $icon)) { throw 'Pacote incompleto: ResMode.ico ausente.' }

# Autotestes antes de mexer na instalação.
foreach ($test in '/selftest', '/preferencestest') {
    $probe = Join-Path $env:TEMP ("$name-test-" + [guid]::NewGuid().ToString('N') + '.txt')
    try {
        $p = Start-Process -FilePath $exe -ArgumentList "$test `"$probe`"" -PassThru -Wait
    } catch { throw "O Windows bloqueou o executável compilado ($($_.Exception.Message)). Veja a seção Smart App Control no README." }
    if ($p.ExitCode -ne 0 -or -not (Test-Path $probe)) { throw "O autoteste $test falhou." }
    Get-Content $probe -Encoding UTF8
    Remove-Item $probe
}

if (Test-Path (Join-Path $installRoot 'restauracao.xml')) { throw 'A resolução de jogo está ativa. Feche o jogo e tente de novo.' }
# Encerra só a cópia instalada (sem jogo aberto, não há resolução para restaurar).
$installedExe = Join-Path $installRoot "$name.exe"
$running = Get-Process -Name $name -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe }
$running | Stop-Process -Force
$running | ForEach-Object { $_.WaitForExit(5000) | Out-Null }
Copy-Item $exe, $icon $installRoot -Force

# Validate destination before stopping the previous utility.
$where = Join-Path $dist 'installed-path.txt'
$p = Start-Process -FilePath (Join-Path $installRoot "$name.exe") -ArgumentList ('/where "' + $where + '"') -PassThru -Wait
if ($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $where)) { throw 'Destino nao confirmado; app anterior preservado.' }
$physicalRoot = (Get-Content -LiteralPath $where -Raw).Trim()
if (-not (Test-Path -LiteralPath (Join-Path $physicalRoot "$name.exe"))) { throw 'Caminho fisico invalido.' }
Copy-Item -LiteralPath (Join-Path $root 'assets\ResMode-logo.png') -Destination $installRoot -Force
$previousStartup = Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Startup')) 'SharpShift.lnk')
if ($PreviousInstallRoot) {
    if (Test-Path -LiteralPath (Join-Path $PreviousInstallRoot 'restauracao.xml')) { throw 'Feche o jogo antes de migrar.' }
    $oldTargets = @((Join-Path $PreviousInstallRoot 'SharpShift.exe'), (Join-Path $env:LOCALAPPDATA 'SharpShift\SharpShift.exe'))
    $oldProcesses = Get-Process -Name SharpShift -ErrorAction SilentlyContinue | Where-Object { $oldTargets -contains $_.Path }
    $oldProcesses | Stop-Process -Force
    $oldProcesses | ForEach-Object { $_.WaitForExit(5000) | Out-Null }
    if (-not (Test-Path -LiteralPath (Join-Path $installRoot 'jogos.xml'))) {
        foreach ($file in 'jogos.xml','exclusoes.xml','catalogo.xml','barra-pendente.txt','pastas-restauracao.xml') {
            $old = Join-Path $PreviousInstallRoot $file
            if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $installRoot }
        }
        $oldLog = Join-Path $PreviousInstallRoot 'SharpShift.log'
        if (Test-Path -LiteralPath $oldLog) { Copy-Item -LiteralPath $oldLog -Destination (Join-Path $installRoot "$name.log") }
    }
}

# Atalhos: área de trabalho sempre; inicialização só na primeira instalação ou se já estava ligada.
$startup = [Environment]::GetFolderPath('Startup')
$startupLink = Join-Path $startup "$name.lnk"
$firstInstall = -not (Test-Path (Join-Path $installRoot 'jogos.xml'))
$shell = New-Object -ComObject WScript.Shell
foreach ($folder in [Environment]::GetFolderPath('Desktop'), $startup) {
    $file = Join-Path $folder "$name.lnk"
    if ($folder -eq $startup -and -not $firstInstall -and -not (Test-Path $startupLink) -and -not $previousStartup) { continue }
    $link = $shell.CreateShortcut($file)
    $link.TargetPath = Join-Path $physicalRoot "$name.exe"
    $link.WorkingDirectory = $physicalRoot
    $link.Arguments = if ($folder -eq $startup) { '/background' } else { '' }
    $link.Description = 'Resolução de jogo automática; volta ao desktop ao fechar.'
    $link.IconLocation = (Join-Path $physicalRoot "$name.exe") + ',0'
    $link.Save()
    $oldLink = Join-Path $folder 'SharpShift.lnk'
    if (Test-Path -LiteralPath $oldLink) { Remove-Item -LiteralPath $oldLink }
}

if (-not $NoStart) { Start-Process (Join-Path $physicalRoot "$name.exe") }
Write-Output "Instalado em $installRoot"
