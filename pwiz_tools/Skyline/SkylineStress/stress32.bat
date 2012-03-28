@echo off
setlocal
@echo off

set STRESS_ROOT=%~dp0
%STRESS_ROOT%..\bin\x86\Release\SkylineStress %*
