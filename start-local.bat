@echo off
title CropProgress Monitor - Local Host
echo Starting CropProgress Monitor on http://localhost:5052 ...
start "" "http://localhost:5052"
dotnet run --project "%~dp0CropProgressMonitor\CropProgressMonitor.csproj" --urls "http://localhost:5052"
pause
