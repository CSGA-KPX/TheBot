@echo off

set git_proxy_host=localhost
set git_proxy_port=1080
set http_proxy=http://%git_proxy_host%:%git_proxy_port%
set https_proxy=http://%git_proxy_host%:%git_proxy_port%

set /P download=是否下载数据文件：(Y/N)
if NOT "%download%"=="Y" GOTO :Diff

echo 正在下载数据文件

wget https://github.com/thewakingsands/ffxiv-datamining-cn/archive/refs/heads/master.zip -O ffxiv-datamining-cn-master.zip
wget https://github.com/MansonGit/ffxiv-datamining-jp/archive/refs/heads/main.zip -O ffxiv-datamining-ja-master.zip

:Diff

set /P download=是否下载定义文件：(Y/N)
if NOT "%download%"=="Y" GOTO :END

rd patchdata /s /q 1>nul 2>nul
del ffxiv-datamining-patchdiff.zip  1>nul 2>nul

echo 下载差分文件

git clone -n --depth=1 --filter=tree:0 https://github.com/xivapi/ffxiv-datamining-patches/
cd ffxiv-datamining-patches
git sparse-checkout set --no-cone patchdata
git checkout
move /Y patchdata ..\
cd ..
rd /s /q ffxiv-datamining-patches 1>nul 2>nul

echo 下载版本定义
curl https://xivapi.com/patchlist > patchdata\PatchVersion.json

powershell Compress-Archive -CompressionLevel Fastest -Force patchdata\* ffxiv-datamining-patchdiff.zip

rd patchdata /s /q 1>nul 2>nul

:END
pause

