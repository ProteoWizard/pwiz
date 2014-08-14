@echo off
setlocal
@echo off

set RUNNER_ROOT=%~dp0
%RUNNER_ROOT%..\bin\x86\Release\TestRunner %*
