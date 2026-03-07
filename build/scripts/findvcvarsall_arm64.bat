@ECHO OFF
SETLOCAL ENABLEDELAYEDEXPANSION
REM Workaround for Native AOT on ARM64: vcvarsall arm64 does not add Hostarm64\arm64 to PATH.
REM This script finds the ARM64 VC tools and outputs: CppToolsDirectory#LIB (same format as findvcvarsall.bat).

SET "vswherePath=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
IF NOT EXIST "%vswherePath%" EXIT /B 1

FOR /F "tokens=*" %%i IN (
  '"%vswherePath%" -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.ARM64 -property installationPath'
) DO SET "vsBase=%%i"

IF "%vsBase%"=="" EXIT /B 1

REM Find first MSVC version folder (e.g. 14.50.35717)
FOR /F "tokens=*" %%v IN ('dir /b "%vsBase%\VC\Tools\MSVC" 2^>nul') DO (
  SET "toolsDir=%vsBase%\VC\Tools\MSVC\%%v\bin\Hostarm64\arm64\"
  SET "msvcLib=%vsBase%\VC\Tools\MSVC\%%v\lib\arm64"
  IF EXIST "!toolsDir!link.exe" (
    REM Initialize VC env so LIB is set (on ARM64 vcvarsall often omits MSVC lib\arm64 where LIBCMT.lib lives)
    CALL "%vsBase%\VC\Auxiliary\Build\vcvarsall.bat" arm64 >NUL 2>&1
    IF "!LIB!"=="" (SET "LIB=!msvcLib!") ELSE (SET "LIB=!msvcLib!;!LIB!")
    ECHO !toolsDir!#!LIB!
    EXIT /B 0
  )
)
EXIT /B 1
