@echo off
echo Compiling WuRuSwitch...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:logo.ico /out:WuRuSwitch.exe Program.cs
echo Build completed!
pause
