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
BOOST_BUILD_PATH=$PWIZ_ROOT/libraries/boost-build
PWIZ_BJAM_PATH=$BOOST_BUILD_PATH/engine/bin/$(uname -s)
PWIZ_BJAM=$PWIZ_BJAM_PATH/bjam

# Build local copy of bjam
if [ ! -e $PWIZ_BJAM ]; then
    echo "Building bjam..."
    cd $BOOST_BUILD_PATH/engine
    LOCATE_TARGET=bin sh build.sh
    mkdir -p $PWIZ_BJAM_PATH
    cp -f $BOOST_BUILD_PATH/engine/bin/bjam $PWIZ_BJAM
fi

#if $(hash setarch > /dev/null 2>&1); then
#   ADDRESS_MODEL=$(expr "$*" : '.*address-model=\([36][24]\).*');
#   if [ $ADDRESS_MODEL ]; then
#      SETARCH="setarch linux$ADDRESS_MODEL";
#   fi
#fi

# Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo "Building pwiz..."
cd $PWIZ_ROOT
if ! BOOST_BUILD_PATH=$BOOST_BUILD_PATH $PWIZ_BJAM "$@"; then
   echo "At least one pwiz target failed to build."
   exit 1
fi
