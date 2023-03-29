@echo off
rd patchdata /s /q 1>nul 2>nul
del ffxiv-datamining-patchdiff.zip  1>nul 2>nul

echo 下载差分文件
svn checkout --config-option servers:global:http-proxy-host=localhost   --config-option servers:global:http-proxy-port=1080 https://github.com/xivapi/ffxiv-datamining-patches/trunk/patchdata

echo 下载版本定义
curl -6 https://xivapi.com/patchlist > patchdata\PatchVersion.json

"C:\Program Files\Bandizip\Bandizip.exe" a -ex:.svn ffxiv-datamining-patchdiff.zip patchdata\

rd patchdata /s /q 1>nul 2>nul