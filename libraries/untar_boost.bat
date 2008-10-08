pushd %1\libraries
IF EXIST boost_1_36_0/boost/algorithm/string.hpp GOTO SKIP
echo Extracting tarballs...
bsdtar.exe -xkjvf boost_1_36_0.tar.bz2 boost_1_36_0/libs boost_1_36_0/boost boost_1_36_0/tools/jam boost_1_36_0/tools/build
:SKIP
popd