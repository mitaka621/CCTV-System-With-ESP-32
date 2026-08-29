@echo off
setlocal

set "SOLUTION_DIR=%~dp0"
set "PROJECT_DIR=%SOLUTION_DIR%BlazorCamPortal"
set "PUBLISH_DIR=%PROJECT_DIR%\publish"
set "APP_EXE=%PUBLISH_DIR%\CamPortal.exe"
set "LOG_DIR=%PROJECT_DIR%\logs"

set "ASPNETCORE_ENVIRONMENT=Production"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"

rem publish output is replaced on every boot, so persistent state stays in the project folder
set "ServerStorage__RootPath=%PROJECT_DIR%"
set "ServerIdentity__PrivateKeyPath=%PROJECT_DIR%\data\server-identity.pem"
set "DeviceTypeIconsConfig__IconsFolder=%PROJECT_DIR%\device-type-icons"

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [%date% %time%] Publishing Release...>> "%LOG_DIR%\startup.log"
dotnet publish "%PROJECT_DIR%\CamPortal.csproj" -c Release -o "%PUBLISH_DIR%" --nologo > "%LOG_DIR%\build.log" 2>&1
if errorlevel 1 (
    echo [%date% %time%] PUBLISH FAILED - see build.log - not starting.>> "%LOG_DIR%\startup.log"
    exit /b 1
)

if not exist "%APP_EXE%" (
    echo [%date% %time%] MISSING %APP_EXE% - not starting.>> "%LOG_DIR%\startup.log"
    exit /b 1
)

cd /d "%PUBLISH_DIR%"

echo [%date% %time%] Starting CamPortal...>> "%LOG_DIR%\startup.log"
"%APP_EXE%" >> "%LOG_DIR%\app.log" 2>&1
set "EXIT_CODE=%errorlevel%"
echo [%date% %time%] CamPortal exited with code %EXIT_CODE%.>> "%LOG_DIR%\startup.log"
exit /b %EXIT_CODE%
