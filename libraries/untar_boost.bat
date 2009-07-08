pushd %1\libraries
IF EXIST boost_1_39_0/boost/algorithm/string.hpp GOTO SKIP
echo Extracting boost tarball...
bsdtar.exe -xkjf boost_1_39_0.tar.bz2
:SKIP
popd
