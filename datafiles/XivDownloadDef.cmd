@echo off

echo ���ű���������SaintCoinach��json�����ļ�
echo ������Ŀ����ʧ�ܷ�����Ҫ��������ļ�
echo ���غ���Ҫ�Ѷ�Ӧ�ļ�����ӵ���Ӧ��zip�ļ���
echo ��ffxiv-datamining-ja-master.zip/ffxiv-datamining-jp-main/Definitions

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