@echo off
svn checkout --config-option servers:global:http-proxy-host=localhost   --config-option servers:global:http-proxy-port=1080 https://github.com/xivapi/SaintCoinach/trunk/SaintCoinach/Definitions
pause