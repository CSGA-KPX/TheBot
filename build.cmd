@echo off

dotnet tool restore
dotnet restore

dotnet build
pushd build\debug
call TheBotData.exe
if ERRORLEVEL 1 (
    echo 数据缓存生成失败
    pause
    goto :EOF
)
call TheBot.exe runCmdTest:
if ERRORLEVEL 1 (
    echo 数据缓存生成失败
    pause
    goto :EOF
)
popd
pause
goto :EOF

dotnet test /p:AltCover=true "/p:AltCoverTypeFilter=?KPX|ProviderImplementation.ProvidedTypes|LibFFXIV.GameData.Provider|StartupCode" "/p:AltCoverAssemblyExcludeFilter=Test|test" "/p:AltCoverReport=$(SolutionDir)/tmp/coverage_report/$(ProjectName).coverage.xml" -v n

dotnet reportgenerator "-reports:./tmp/coverage_report/*.xml" "-targetdir:./tmp/coverage_html/" "-sourcedirs:./src/;../LibFFXIV/" -reporttypes:Html

rem git clone -b "gh-pages" . tmp/gh-pages
rem xcopy /Y /E output\ tmp\gh-pages\
rem pushd tmp\gh-pages
rem git commit -a -m "Update generated documentation."
rem git push
rem popd