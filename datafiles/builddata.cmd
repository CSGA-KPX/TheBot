@echo off
set http_proxy=http://localhost:1080
set https_proxy=http://localhost:1080

wget https://github.com/thewakingsands/ffxiv-datamining-cn/archive/refs/heads/master.zip -O ffxiv-datamining-cn-master.zip
wget https://github.com/MansonGit/ffxiv-datamining-jp/archive/refs/heads/main.zip -O ffxiv-datamining-ja-master.zip

pause