@echo off
setlocal EnableExtensions

set "PACKAGE_DIR=%~dp0"
set "INSTALLER=%PACKAGE_DIR%ResoDrive-Setup.msi"
set "PROFILE_SOURCE=%PACKAGE_DIR%profiles.json"
set "PROFILE_DIR=%LOCALAPPDATA%\rdrive"
set "PROFILE_TARGET=%PROFILE_DIR%\profiles.json"
set "PROFILE_TEMP=%PROFILE_DIR%\profiles.json.new"
set "APP=%ProgramFiles%\rdrive\resodrive.exe"

if not exist "%INSTALLER%" (
  echo ResoDrive-Setup.msi was not found beside this installer.
  goto :fail
)

if not exist "%PROFILE_SOURCE%" (
  echo profiles.json was not found beside this installer.
  goto :fail
)

echo Installing ResoDrive...
start /wait "" msiexec.exe /i "%INSTALLER%" /passive /norestart
set "INSTALL_RESULT=%ERRORLEVEL%"
if not "%INSTALL_RESULT%"=="0" if not "%INSTALL_RESULT%"=="3010" (
  echo ResoDrive installation failed with Windows Installer code %INSTALL_RESULT%.
  goto :fail
)

if exist "%PROFILE_TARGET%" (
  echo Keeping the existing user profile at:
  echo %PROFILE_TARGET%
) else (
  if not exist "%PROFILE_DIR%" mkdir "%PROFILE_DIR%"
  if errorlevel 1 (
    echo The user profile directory could not be created.
    goto :fail
  )

  copy /y "%PROFILE_SOURCE%" "%PROFILE_TEMP%" >nul
  if errorlevel 1 (
    echo The supplied profile could not be copied.
    goto :fail
  )

  move /y "%PROFILE_TEMP%" "%PROFILE_TARGET%" >nul
  if errorlevel 1 (
    echo The supplied profile could not be activated.
    goto :fail
  )
  echo Installed the user profile at:
  echo %PROFILE_TARGET%
)

if exist "%APP%" start "" "%APP%"
echo ResoDrive is ready.
if "%INSTALL_RESULT%"=="3010" echo Windows requested a restart to complete installation.
exit /b 0

:fail
echo.
echo ResoDrive was not fully installed. No existing profile was changed.
pause
exit /b 1
