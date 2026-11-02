@echo off
rem Dois cliques para instalar. A politica de execucao e liberada so para este script, nesta janela.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar.ps1"
echo.
pause
