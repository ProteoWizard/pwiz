#!/bin/sh

cd $1/libraries;
if [ ! -f fftw-3.1.2/README ]; then
	echo "Extracting fftw3 tarball..."
	tar -xkjf fftw-3.1.2.tar.bz2
fi
