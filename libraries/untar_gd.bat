if "%1" == "" GOTO SKIP_PUSH
pushd %1\libraries
:SKIP_PUSH

IF EXIST gd-2.0.33/wbmp.h GOTO SKIP
echo Extracting gd headers...
bsdtar.exe -xkjf gd-2.0.33.tar.bz2 *.h
:SKIP

if "%1" == "" GOTO SKIP_POP
popd
:SKIP_POP