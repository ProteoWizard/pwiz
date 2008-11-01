pushd %1\libraries
IF EXIST zlib-1.2.3/zutil.h GOTO SKIP
echo Extracting zlib tarball...
bsdtar.exe -xkjvf zlib-1.2.3.tar.bz2
:SKIP
popd