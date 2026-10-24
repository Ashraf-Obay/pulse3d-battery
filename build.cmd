@echo off
rem Builds Pulse3DBattery.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ ^
  /out:"%~dp0Pulse3DBattery.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0Pulse3DBattery.cs" "%~dp0IconArt.cs"
