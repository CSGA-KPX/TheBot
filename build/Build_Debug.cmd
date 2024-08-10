@echo off
call ZConfig.cmd
rd .\bin\ /S /Q
mkdir bin
pushd ..
dotnet clean
cls
dotnet build
popd
pause
pushd bin\
thebot REPL: runCommand:##rebuilddatacache
popd
pause