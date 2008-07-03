cd %1/libraries
IF EXIST boost_1_34_1/boost/algorithm/string.hpp GOTO SKIP
echo "Extracting tarballs..."
bsdtar.exe -xkjvf boost_1_34_1.tar.bz2 boost_1_34_1/libs boost_1_34_1/boost
:SKIP