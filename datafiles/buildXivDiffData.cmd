@echo off
rd patchdata /s /q 1>nul 2>nul
del patchdata.zip  1>nul 2>nul

echo 下载差分文件
svn checkout --config-option servers:global:http-proxy-host=localhost   --config-option servers:global:http-proxy-port=1081 https://github.com/xivapi/ffxiv-datamining-patches/trunk/patchdata

echo 下载定义文件
curl -6 https://raw.githubusercontent.com/xivapi/ffxiv-datamining-patches/master/build.php > patchdata\build.php

"C:\Program Files\Bandizip\Bandizip.exe" a -ex:.svn ffxiv-datamining-patchdiff.zip patchdata\

rd patchdata /s /q 1>nul 2>nul