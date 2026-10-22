@echo off
chcp 65001 >nul
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0代码\工具\启动游戏.ps1" %*
if errorlevel 1 (
  echo.
  pause
  exit /b 1
)
endlocal
