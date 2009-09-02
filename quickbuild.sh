#!/bin/sh
#
# script for getting bjam, boost and ProteoWizard up and running
#
# Get the location of quickbuild.sh and drop trailing slash
PWIZ_ROOT=$(pwd)
if [ ! -e $PWIZ_ROOT/quickbuild.sh ]
then
echo "quickbuild.sh must be run from the directory it resides in - quitting"
exit 1
fi

case "$1" in
  "mingw" ) 
	BJAM_BIN=bin.ntx86 
	BJAM_TOOLSET="mingw"
	PWIZ_TOOLSET="gcc"
	;;
  "linuxx86" ) 
	BJAM_BIN=bin.linuxx86 
	BJAM_TOOLSET="gcc"
	PWIZ_TOOLSET="gcc"
	;;
  "linuxx86_64" ) 
	BJAM_BIN=bin.linuxx86_64 
	BJAM_TOOLSET="gcc"
	PWIZ_TOOLSET="gcc"
	;;
  "darwin" ) 
	BJAM_BIN=bin.macosxx86 
	BJAM_TOOLSET="darwin"
	PWIZ_TOOLSET="darwin"
	;;
   *  ) 
	echo "usage: quickbuild.sh mingw|linuxx86|linuxx86_64|darwin [optional_bjam_args]"
	exit 1
	;;
esac

# Extract Boost distro
$PWIZ_ROOT/libraries/untar_boost.sh  $PWIZ_ROOT

# Extract Boost.Build
if [ ! -e $PWIZ_ROOT/libraries/boost-build/jam_src/build.sh ]
then
echo "extracting boost-build..."
cd $PWIZ_ROOT/libraries ; tar -xkjf boost-build.tar.bz2
fi
cp $PWIZ_ROOT/libraries/msvc.jam $PWIZ_ROOT/libraries/boost-build/tools

PWIZ_BJAM=$PWIZ_ROOT/libraries/boost-build/jam_src/$BJAM_BIN/bjam

# Build local copy of bjam
if [ ! -e $PWIZ_BJAM ]
then
echo "Building bjam..."
cd $PWIZ_ROOT/libraries/boost-build/jam_src; sh build.sh $BJAM_TOOLSET
fi

# Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo "Building pwiz..."
echo "cd $PWIZ_ROOT ; export BOOST_BUILD_PATH=$PWIZ_ROOT/libraries/boost-build ; $PWIZ_BJAM $2 $3 $4 $5 toolset=$PWIZ_TOOLSET $PWIZ_ROOT"
cd $PWIZ_ROOT ; export BOOST_BUILD_PATH=$PWIZ_ROOT/libraries/boost-build 
if ! $PWIZ_BJAM $2 $3 $4 $5 "toolset=$PWIZ_TOOLSET" $PWIZ_ROOT
then
	echo "BJAM build failed!"
	exit 1
fi
