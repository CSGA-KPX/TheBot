@echo off
call ZConfig.cmd
set WSLENV=endpoint/u:token/u
pushd bin
bash -c "dotnet thebot.dll"
pause