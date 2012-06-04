#!/bin/sh
#
# script for getting bjam and bumbershoot up and running
#
# Get the location of quickbuild.sh and drop trailing slash
ROOT=$(pwd)
if [ ! -e $ROOT/quickbuild.sh ]; then
    echo "quickbuild.sh must be run from the directory it resides in - quitting"
    exit 1
fi

BJAM=$ROOT/freicore/libraries/boost-build/jam_src/bin/bjam

# Build local copy of bjam
if [ ! -e $BJAM ]; then
    echo "Building bjam..."
    cd $ROOT/freicore/libraries/boost-build/jam_src
    LOCATE_TARGET=bin sh build.sh
fi

if test $(uname -s) = "Darwin"; then
    DEFAULT_TOOLSET=toolset=darwin
fi
    
# Do full build, passing quickbuild's arguments to bjam

cd $ROOT
if ! BOOST_BUILD_PATH=$ROOT/freicore/libraries/boost-build $BJAM $DEFAULT_TOOLSET "$@"; then
	  echo "This bumbershoot target failed to build."
	  exit 1
fi
