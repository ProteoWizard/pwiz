#!/bin/sh

cd $1/libraries;
if [ ! -e boost-build/jam_src/build.sh ]; then
	echo "Extracting boost-build tarball...";
	tar -xkjf boost-build.tar.bz2
fi;
