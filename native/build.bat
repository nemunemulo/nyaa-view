@echo off
setlocal
cd /d "%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUT=%~dp0..\nyaa-view.exe"

echo [nyaa-view] Compiling native Windows 11 executable...
"%CSC%" /target:winexe /optimize+ /platform:anycpu /out:"%OUT%" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xaml.dll /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll" /r:"C:\Windows\System32\WinMetadata\Windows.Data.winmd" /r:"C:\Windows\System32\WinMetadata\Windows.Storage.winmd" /r:"C:\Windows\System32\WinMetadata\Windows.Foundation.winmd" "%~dp0*.cs"

if %ERRORLEVEL% EQU 0 (
    echo [nyaa-view] Compilation successful!
    echo Binary: %OUT%
) else (
    echo [nyaa-view] Compilation failed!
)
endlocal
