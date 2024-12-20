@echo off

set git_proxy_host=localhost
set git_proxy_port=1080
set http_proxy=http://%git_proxy_host%:%git_proxy_port%
set https_proxy=http://%git_proxy_host%:%git_proxy_port%
set bandizip=bandizip.exe

set /P download=是否下载数据文件：(Y/N)
if NOT "%download%"=="Y" GOTO :Diff

echo 正在下载数据文件

wget https://github.com/thewakingsands/ffxiv-datamining-cn/archive/refs/heads/master.zip -O ffxiv-datamining-cn-master.zip
wget https://github.com/Souma-Sumire/ffxiv-datamining-hexcode-ja/archive/refs/heads/main.zip -O ffxiv-datamining-ja-master.zip

:Diff

set /P download=是否下载定义文件：(Y/N)
if NOT "%download%"=="Y" GOTO :END

rd patchdata /s /q 1>nul 2>nul
del ffxiv-datamining-patchdiff.zip  1>nul 2>nul

echo 下载差分文件

git clone -n --depth=1 --filter=tree:0 https://github.com/xivapi/ffxiv-datamining-patches/
cd ffxiv-datamining-patches
git checkout
move /Y patchlist.json patchdata\PatchVersion.json
move /Y patchdata ..\
cd ..
rd /s /q ffxiv-datamining-patches 1>nul 2>nul

bandizip a -r -storeroot:no - "ffxiv-datamining-patchdiff.zip" patchdata\
rem powershell Compress-Archive -CompressionLevel NoCompression -Force patchdata\* ffxiv-datamining-patchdiff.zip

rd patchdata /s /q 1>nul 2>nul

:END
pause

