@echo off
cd /d "%~dp0"
start "Campus Echo - Teaching" "Builds\Windows\CampusEcho.exe" -teachingPreview -force-d3d11 -screen-fullscreen 0
