@echo off
setlocal
title Sales Lead Management Tool - Demo

REM --- Banner: what this script does ---
echo.
echo ========================================
echo   Sales Lead Management Tool - One-click demo launcher
echo ========================================
echo.

REM --- Require dotnet CLI + any SDK 8+ (9.x SDK still builds net8.0 projects) ---
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet CLI not found. Install a .NET SDK ^(8 or newer^) and ensure it is on PATH.
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
set "HAS_SDK_GE8=0"
for /f "tokens=1" %%a in ('dotnet --list-sdks 2^>nul') do (
  for /f "tokens=1 delims=." %%m in ("%%a") do (
    if %%m geq 8 set "HAS_SDK_GE8=1"
  )
)
if not "%HAS_SDK_GE8%"=="1" (
  echo ERROR: No .NET SDK 8 or newer found. This solution targets .NET 8.
  echo Run: dotnet --list-sdks
  echo Install from: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)

cd /d "%~dp0"

REM --- Restore, build, pick port, run API, health-check, open Swagger ---
echo [1/4] Restoring packages...
dotnet restore
if errorlevel 1 goto :error

echo [2/4] Building solution...
dotnet build
if errorlevel 1 goto :error

REM --- Prefer 5055; if busy, next free loopback TCP port (bind probe) ---
set "PORTFILE=%TEMP%\SalesLeadDemoPort.txt"
del "%PORTFILE%" 2>nul
echo [3/4] Choosing a free port ^(prefers 5055^)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$path=[IO.Path]::Combine($env:TEMP,'SalesLeadDemoPort.txt'); if(Test-Path $path){Remove-Item $path -Force}; $p=5055; while($p -lt 65535){$l=$null; try{$l=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$p); $l.Start(); $l.Stop(); [IO.File]::WriteAllText($path,$p.ToString()); exit 0} catch { if($null -ne $l){ try{$l.Stop()} catch{} } }; $p++}; exit 1"
if errorlevel 1 (
  echo ERROR: No free TCP port found in range 5055-65534.
  goto :error
)
set "DEMO_PORT="
for /f "usebackq delims=" %%p in ("%PORTFILE%") do set "DEMO_PORT=%%p"
if not defined DEMO_PORT (
  echo ERROR: Could not read chosen port from temp file.
  goto :error
)
echo        Using http://localhost:%DEMO_PORT%

echo [4/4] Starting API in a new terminal...
start "SalesLead API" cmd /k "cd /d ""%~dp0"" && set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:%DEMO_PORT% && dotnet run --no-build --project .\src\SalesLead.Api\SalesLead.Api.csproj"

echo Waiting for API startup (up to 30s)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(30); $u='http://localhost:%DEMO_PORT%/health/live'; $ok=$false; while((Get-Date) -lt $deadline){ try { Invoke-WebRequest -UseBasicParsing $u -TimeoutSec 2 | Out-Null; $ok=$true; break } catch { Start-Sleep -Milliseconds 750 } }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
  echo API is not reachable on http://localhost:%DEMO_PORT%.
  echo Please check the "SalesLead API" terminal for startup errors.
  pause
  exit /b 1
)

echo Opening Swagger...
start "" "http://localhost:%DEMO_PORT%/swagger"

echo Done. Keep the "SalesLead API" terminal open while using Swagger.
exit /b 0

:error
echo Failed to run. Check errors above.
pause
exit /b 1
