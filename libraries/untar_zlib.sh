#!/bin/sh

cd $1/libraries;
if [ ! -e zlib-1.2.3/zutil.h ]; then
	echo "Extracting zlib tarball...";
	tar -xkjvf zlib-1.2.3.tar.bz2;
fi;
