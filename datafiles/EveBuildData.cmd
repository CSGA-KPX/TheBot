@echo off
set Python27=Z:\@Area\Source\BotData\python-2.7.13.amd64\python.exe
set Phobos=Z:\@Area\Source\BotData\Phobos-2.1.0\run.py
set EvePath=G:\EVE
set EveServer=serenity
set EveLang=zh

rd eve-dump\ /S /Q 1>nul 2>nul
mkdir eve-dump
"%Python27%" "%Phobos%" -e "%EvePath%" -s %EveServer% -t %EveLang% -j eve-dump --list "blueprints, categories, groups, types, industry_activities, marketgroups, npccorporations, schematics, typematerials"

rd eve-data\ /S /Q 1>nul 2>nul
mkdir eve-data

copy EveSolarSystem.tsv eve-data\SolarSystem.tsv
copy eve-dump\fsd_lite\blueprints.json  eve-data\
copy eve-dump\fsd_binary\categories.json  eve-data\evecategories.json
copy eve-dump\fsd_binary\groups.json eve-data\evegroups.json
copy eve-dump\fsd_binary\types.json eve-data\evetypes.json
copy eve-dump\fsd_lite\industry_activities.json eve-data\
copy eve-dump\fsd_binary\marketgroups.json eve-data\
copy eve-dump\fsd_binary\npccorporations.json eve-data\
copy eve-dump\fsd_binary\schematics.json eve-data\
copy eve-dump\fsd_binary\typematerials.json eve-data\

powershell Compress-Archive -CompressionLevel Fastest -Force eve-data\* EVEData.zip

rd eve-data\ /S /Q 1>nul 2>nul
rd eve-dump\ /S /Q 1>nul 2>nul

pause