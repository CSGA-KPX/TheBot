@echo off
call ZConfig.cmd
pushd bin\
thebot REPL: runCommand:##rebuilddatacache
popd
pause