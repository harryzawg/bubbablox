@echo off
cd C:\Users\Admin\Desktop\Revival\ecsr\ecsrev-main\services2021
taskkill /f /im RCCService.exe
start /b cmd /c "cd /d 2016-roblox-main && call run.bat"
start /b cmd /c "cd /d RCCService && call run.bat"
timeout /t 2 >nul
start /b cmd /c "cd /d renderer && call run.bat"
start /b cmd /c "cd /d AssetValidationServiceV2 && call run.bat"
start cmd /c "cd /d Roblox/Roblox.Website && run.bat"
start /b redis-server.exe
