@echo off

echo 本脚本用于下载SaintCoinach的json定义文件
echo 除非项目编译失败否则不需要下载相关文件
echo 下载后需要把对应文件夹添加到对应的zip文件中

pause

set git_proxy_host=localhost
set git_proxy_port=1080

svn checkout --config-option servers:global:http-proxy-host=%git_proxy_host%   --config-option servers:global:http-proxy-port=%git_proxy_port% https://github.com/xivapi/SaintCoinach/trunk/SaintCoinach/Definitions
pause