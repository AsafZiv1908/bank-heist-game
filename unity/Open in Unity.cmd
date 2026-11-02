@echo off
set "UNITY_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe"
if exist "%UNITY_EDITOR%" (
  start "Bank Heist 3D" "%UNITY_EDITOR%" -projectPath "%~dp0."
) else (
  echo Unity 6000.5.7f1 was not found at the default location.
  echo Open Unity Hub and add this folder as a project.
  pause
)
