#!/bin/sh

cd $1/libraries;

# test for last file extracted so we re-extract after a partial extraction
if [ ! -e boost-build/site-config.jam ]; then
	echo "Extracting boost-build tarball...";
	tar -xkjf boost-build.tar.bz2
fi;

cp -fu testing.jam boost-build/tools