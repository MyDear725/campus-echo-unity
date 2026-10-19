@echo off
cd /d "%~dp0"
start "Campus landscape" "Builds\Windows\CampusEcho.exe" -roadLandscapePreview -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 1000 -logFile "%~dp0road-landscape-preview.log"
