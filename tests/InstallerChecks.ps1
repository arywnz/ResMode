# Runs only on the disposable CI VM. Never install a candidate over a user's app for testing.
param([Parameter(Mandatory=$true)][string]$Msi)
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Installer smoke test is restricted to the disposable GitHub runner.'}
$installed=Join-Path $env:LOCALAPPDATA 'Programs\ResMode\ResMode.exe'
if(Test-Path -LiteralPath $installed){throw 'Refusing to replace a pre-existing installed app.'}
$userdata=Join-Path $env:LOCALAPPDATA 'ResMode'
if(Test-Path -LiteralPath $userdata){throw 'Refusing to alter existing user data.'}
New-Item -ItemType Directory -Path $userdata | Out-Null
$sentinel=Join-Path $userdata 'installer-test.txt'
[IO.File]::WriteAllText($sentinel,'Keep my settings')
$startupLink=Join-Path ([Environment]::GetFolderPath('Startup')) 'ResMode.lnk'
if(Test-Path -LiteralPath $startupLink){throw 'Refusing to replace an existing startup link.'}
$shell=New-Object -ComObject WScript.Shell
$oldLink=$shell.CreateShortcut($startupLink)
$oldLink.TargetPath=Join-Path $userdata 'old-ResMode.exe'
$oldLink.Save()
$log=Join-Path $PSScriptRoot '..\dist\test-results\msi-install.log'
$uninstallLog=Join-Path $PSScriptRoot '..\dist\test-results\msi-uninstall.log'
$arguments='/i "'+[IO.Path]::GetFullPath($Msi)+'" /qn /norestart /l*v "'+$log+'"'
$process=Start-Process msiexec.exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -notin @(0,3010)){throw "MSI install failed: $($process.ExitCode). See $log"}
if(-not (Test-Path -LiteralPath $installed)){throw 'MSI did not install to the per-user directory.'}
if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot '..\dist\ResMode.exe')).Hash){throw 'Installed executable differs from tested build.'}
if(-not (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'ResMode.lnk'))){throw 'Desktop shortcut missing.'}
if([IO.File]::ReadAllText($sentinel) -ne 'Keep my settings'){throw 'Installer changed user data.'}
if($shell.CreateShortcut($startupLink).TargetPath -ne $installed){throw 'Legacy startup link was not migrated to the MSI executable.'}
$arguments='/x "'+[IO.Path]::GetFullPath($Msi)+'" /qn /norestart /l*v "'+$uninstallLog+'"'
$process=Start-Process msiexec.exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -notin @(0,3010)){throw "MSI uninstall failed: $($process.ExitCode)."}
if(Test-Path -LiteralPath $installed){throw 'Uninstall left the application executable.'}
if([IO.File]::ReadAllText($sentinel) -ne 'Keep my settings'){throw 'MSI uninstall deleted user settings.'}
if(Test-Path -LiteralPath $startupLink){throw 'Uninstall left its startup link.'}
Write-Output 'PASS: per-user MSI install/uninstall, exact executable, desktop shortcut, legacy startup migration and retained user data. App was not started.'
