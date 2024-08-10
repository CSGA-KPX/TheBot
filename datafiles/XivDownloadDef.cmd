@echo off

echo 本脚本用于下载SaintCoinach的json定义文件
echo 除非项目编译失败否则不需要下载相关文件
echo 下载后需要把对应文件夹添加到对应的zip文件中
echo 如ffxiv-datamining-ja-master.zip/ffxiv-datamining-jp-main/Definitions

pause

set git_proxy_host=localhost
set git_proxy_port=1080

REM svn checkout --config-option servers:global:http-proxy-host=%git_proxy_host%   --config-option servers:global:http-proxy-port=%git_proxy_port% https://github.com/xivapi/SaintCoinach/trunk/SaintCoinach/Definitions

git clone -n --depth=1 --filter=tree:0 https://github.com/xivapi/SaintCoinach
cd SaintCoinach
git sparse-checkout set --no-cone SaintCoinach/Definitions
git checkout
rd /s /q ..\Definitions 1>nul 2>nul
move /Y SaintCoinach\Definitions ..\
cd ..
rd /s /q SaintCoinach


pause