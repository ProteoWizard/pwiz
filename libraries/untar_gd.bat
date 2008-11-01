pushd %1\libraries
IF EXIST gd-2.0.33/wbmp.h GOTO SKIP
echo Extracting gd headers...
bsdtar.exe -xkzvf gd-2.0.33.tar.gz *.h
:SKIP
popd