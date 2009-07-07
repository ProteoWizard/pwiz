pushd %1\libraries
IF EXIST gd-2.0.33/wbmp.h GOTO SKIP
echo Extracting gd headers...
bsdtar.exe -xkjvf gd-2.0.33.tar.bz2 *.h
:SKIP
popd