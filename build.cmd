@echo off

dotnet tool restore
dotnet restore

dotnet build

dotnet test /p:AltCover=true