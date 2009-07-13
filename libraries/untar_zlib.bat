if "%1" == "" GOTO SKIP_PUSH
pushd %1\libraries
:SKIP_PUSH

IF EXIST zlib-1.2.3/zutil.h GOTO SKIP
echo Extracting zlib tarball...
bsdtar.exe -xkjf zlib-1.2.3.tar.bz2
:SKIP

if "%1" == "" GOTO SKIP_POP
popd
:SKIP_POP
