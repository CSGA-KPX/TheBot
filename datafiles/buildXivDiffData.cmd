@echo off
rd patchdata /s /q 1>nul 2>nul
del ffxiv-datamining-patchdiff.zip  1>nul 2>nul

echo ���ز���ļ�
svn checkout --config-option servers:global:http-proxy-host=localhost   --config-option servers:global:http-proxy-port=1080 https://github.com/xivapi/ffxiv-datamining-patches/trunk/patchdata

echo ���ذ汾����
curl -6 https://xivapi.com/patchlist > patchdata\PatchVersion.json

"C:\Program Files\Bandizip\Bandizip.exe" a -ex:.svn ffxiv-datamining-patchdiff.zip patchdata\

rd patchdata /s /q 1>nul 2>nul