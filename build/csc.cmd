@echo off
setlocal
for /f "usebackq tokens=*" %%i in (`dotnet --version`) do set DOTNET_SDK_VERSION=%%i
dotnet "%ProgramFiles%\dotnet\sdk\%DOTNET_SDK_VERSION%\Roslyn\bincore\csc.dll" %*
exit /b %ERRORLEVEL%
