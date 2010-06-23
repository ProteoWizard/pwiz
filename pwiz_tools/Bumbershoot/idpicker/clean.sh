#!/bin/bash

root=$(dirname $0)
pushd $root > /dev/null

echo "Cleaning project..."
if [ -d build-*-* ]; then rm -fdr build-*-*; fi;
if [ -d freicore/libraries/boost-build/jam_src/bin ]; then rm -fdr freicore/libraries/boost-build/jam_src/bin; fi;
if [ -d freicore/libraries/boost-build/jam_src/bootstrap ]; then rm -fdr freicore/libraries/boost-build/jam_src/bootstrap; fi;
if [ -d freicore/libraries/boost_1_39_0 ]; then rm -fdr freicore/libraries/boost_1_39_0; fi;
if [ -d freicore/libraries/gd-2.0.33 ]; then rm -fdr freicore/libraries/gd-2.0.33; fi;
if [ -d freicore/libraries/zlib-1.2.3 ]; then rm -fdr freicore/libraries/zlib-1.2.3; fi;
if [ -d freicore/libraries/fftw-3.1.2 ]; then rm -fdr freicore/libraries/fftw-3.1.2; fi;
if [ -d freicore/pwiz_src ]; then rm -fdr freicore/pwiz_src; fi;

popd > /dev/null
