@echo off
rd .\bin\ /S /Q
mkdir bin
pushd ..
dotnet clean
dotnet build
popd
pause