#!/bin/sh

cd $1/libraries;
if [ ! -e boost_1_36_0/boost/algorithm/string.hpp ]; then
	echo "Extracting tarballs...";
	tar -xkvjf boost_1_36_0.tar.bz2 boost_1_36_0/libs boost_1_36_0/boost;
fi;
