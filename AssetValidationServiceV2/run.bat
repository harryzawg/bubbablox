@echo off
SETLOCAL

set "Packages=fastapi pydub aiohttp uvicorn"

python --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    powershell -Command "Add-Type -AssemblyName PresentationFramework;[System.Windows.MessageBox]::Show('Python is not installed, please install Python 3.12 as it is required for asset validation.','Error','OK','Error')"
    exit /b 1
)

for %%P in (%Packages%) do (
    python -c "import %%P" 2>nul
    if %ERRORLEVEL% NEQ 0 (
        echo installing package: %%P
        python -m pip install %%P
    ) else (
        echo already installed
    )
)

start "" python images.py
go run main.go
