@echo off
setlocal
echo [INFO] Building AntigravityLauncher.exe using native Windows .NET Framework compiler...

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC_PATH%" (
    echo [ERROR] csc.exe compiler not found on this system!
    pause
    exit /b 1
)

if not exist "dist" mkdir dist

"%CSC_PATH%" /target:winexe /r:System.Windows.Forms.dll,System.Drawing.dll,Microsoft.CSharp.dll /out:"dist\AntigravityLauncher.exe" "src\AntigravityLauncher.cs"

if %errorlevel% equ 0 (
    echo [SUCCESS] Successfully compiled to dist\AntigravityLauncher.exe
    copy /y "scripts\Launch-Antigravity.bat" "dist\" >nul
    copy /y "scripts\Launch-Antigravity.ps1" "dist\" >nul
) else (
    echo [ERROR] Compilation failed!
    pause
    exit /b 1
)

echo [INFO] Build completed.
