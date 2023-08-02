@echo off
call ZConfig.cmd
rd .\bin\ /S /Q
mkdir bin
pushd ..
dotnet clean
dotnet build --configuration Release
popd
pushd bin\
thebot REPL: runCommand:##rebuilddatacache
popd
pause