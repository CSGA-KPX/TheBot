@echo off

dotnet tool restore
dotnet restore

dotnet build

dotnet test /p:AltCover=true "/p:AltCoverTypeFilter=?KPX|ProviderImplementation.ProvidedTypes|LibFFXIV.GameData.Provider|StartupCode" "/p:AltCoverAssemblyExcludeFilter=Test|test" "/p:AltCoverReport=$(SolutionDir)/tmp/coverage_report/$(ProjectName).coverage.xml"

dotnet reportgenerator "-reports:./tmp/coverage_report/*.xml" "-targetdir:./tmp/coverage_html/" "-sourcedirs:./src/;../LibFFXIV/" -reporttypes:Html

rem git clone -b "gh-pages" . tmp/gh-pages
rem xcopy /Y /E output\ tmp\gh-pages\
rem pushd tmp\gh-pages
rem git commit -a -m "Update generated documentation."
rem git push
rem popd