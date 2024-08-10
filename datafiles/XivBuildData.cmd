@echo off

set git_proxy_host=localhost
set git_proxy_port=1080
set http_proxy=http://%git_proxy_host%:%git_proxy_port%
set https_proxy=http://%git_proxy_host%:%git_proxy_port%

set /P download=�Ƿ����������ļ���(Y/N)
if NOT "%download%"=="Y" GOTO :Diff

echo �������������ļ�

wget https://github.com/thewakingsands/ffxiv-datamining-cn/archive/refs/heads/master.zip -O ffxiv-datamining-cn-master.zip
wget https://github.com/MansonGit/ffxiv-datamining-jp/archive/refs/heads/main.zip -O ffxiv-datamining-ja-master.zip

:Diff

set /P download=�Ƿ����ض����ļ���(Y/N)
if NOT "%download%"=="Y" GOTO :END

rd patchdata /s /q 1>nul 2>nul
del ffxiv-datamining-patchdiff.zip  1>nul 2>nul

echo ���ز���ļ�

git clone -n --depth=1 --filter=tree:0 https://github.com/xivapi/ffxiv-datamining-patches/
cd ffxiv-datamining-patches
git sparse-checkout set --no-cone patchdata
git checkout
move /Y patchdata ..\
cd ..
rd /s /q ffxiv-datamining-patches 1>nul 2>nul

echo ���ذ汾����
curl https://xivapi.com/patchlist > patchdata\PatchVersion.json

powershell Compress-Archive -CompressionLevel Fastest -Force patchdata\* ffxiv-datamining-patchdiff.zip

rd patchdata /s /q 1>nul 2>nul

:END
pause

