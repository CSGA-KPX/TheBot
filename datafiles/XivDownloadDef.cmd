@echo off

echo ���ű���������SaintCoinach��json�����ļ�
echo ������Ŀ����ʧ�ܷ�����Ҫ��������ļ�
echo ���غ���Ҫ�Ѷ�Ӧ�ļ�����ӵ���Ӧ��zip�ļ���

pause

set git_proxy_host=localhost
set git_proxy_port=1080

svn checkout --config-option servers:global:http-proxy-host=%git_proxy_host%   --config-option servers:global:http-proxy-port=%git_proxy_port% https://github.com/xivapi/SaintCoinach/trunk/SaintCoinach/Definitions
pause