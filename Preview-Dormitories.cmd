@echo off
if not exist "%~dp0Builds\Windows\CampusEcho.exe" (
  echo Open the Unity project and choose Campus / Build Windows Demo first.
  pause
  exit /b 1
)
start "" "%~dp0Builds\Windows\CampusEcho.exe" -dormPreview -screen-fullscreen 0
