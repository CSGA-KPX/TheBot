@echo off

dotnet tool restore
dotnet restore

dotnet build

dotnet test /p:AltCover=true "/p:AltCoverTypeFilter=?KPX|ProviderImplementation.ProvidedTypes|LibFFXIV.GameData.Provider|StartupCode" "/p:AltCoverAssemblyExcludeFilter=Test|test"