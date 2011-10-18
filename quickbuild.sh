#!/bin/sh
#
# script for getting bjam and ProteoWizard up and running
#
# Get the location of quickbuild.sh and drop trailing slash
PWIZ_ROOT=$(pwd)
if [ ! -e $PWIZ_ROOT/quickbuild.sh ]; then
    echo "quickbuild.sh must be run from the directory it resides in - quitting"
    exit 1
fi

# per platform in case of multi OS shared volume (VMware etc)
PWIZ_BJAM=$PWIZ_ROOT/libraries/boost-build/jam_src/bin/$(uname -s)/bjam

# Build local copy of bjam
if [ ! -e $PWIZ_BJAM ]; then
    echo "Building bjam..."
    cd $PWIZ_ROOT/libraries/boost-build/jam_src
    LOCATE_TARGET=bin sh build.sh
    mkdir -p $PWIZ_ROOT/libraries/boost-build/jam_src/bin/$(uname -s)
    cp -f $PWIZ_ROOT/libraries/boost-build/jam_src/bin/bjam $PWIZ_ROOT/libraries/boost-build/jam_src/bin/$(uname -s)/bjam
fi

if test $(uname -s) = "Darwin"; then
    DEFAULT_TOOLSET=toolset=darwin
fi
    
# Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo "Building pwiz..."
cd $PWIZ_ROOT
if ! BOOST_BUILD_PATH=$PWIZ_ROOT/libraries/boost-build $PWIZ_BJAM $DEFAULT_TOOLSET "$@"; then
	  echo "At least one pwiz target failed to build."
	  exit 1
fi
