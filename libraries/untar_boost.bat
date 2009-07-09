pushd %1\libraries
IF EXIST boost_1_39_0/boost/algorithm/string.hpp GOTO SKIP
echo Extracting boost tarball...
bsdtar.exe -xkjf boost_1_39_0.tar.bz2 boost_1_39_0/libs boost_1_39_0/boost boost_1_39_0/tools/jam boost_1_39_0/tools/build
:SKIP
popd
