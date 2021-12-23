@echo off
set http_proxy=http://localhost:1081
set https_proxy=http://localhost:1081

wget https://github.com/thewakingsands/ffxiv-datamining-cn/archive/refs/heads/master.zip -O ffxiv-datamining-cn-master.zip
wget https://github.com/suzutan/ffxiv-datamining-jp/archive/refs/heads/master.zip -O ffxiv-datamining-ja-master.zip

pause