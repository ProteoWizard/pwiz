pushd %1
IF EXIST CompassDataTest.data GOTO SKIP
%2\libraries\bsdtar.exe -xkjvf CompassDataTest.data.tar.bz2
:SKIP
popd