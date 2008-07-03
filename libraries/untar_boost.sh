#!/bin/sh

cd $1/libraries;
if [ ! -e boost_1_34_1/boost/algorithm/string.hpp ]; then
	echo "Extracting tarballs...";
	bsdtar -xkvjf boost_1_34_1.tar.bz2 boost_1_34_1/libs boost_1_34_1/boost;
fi;