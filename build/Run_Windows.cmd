@echo off
call ZConfig.cmd
pushd bin
dotnet thebot.dll
pause