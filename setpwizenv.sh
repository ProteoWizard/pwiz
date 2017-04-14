#!/bin/bash
#
# setpwizenv.sh
#
# This script sets up the shell environment to use the local pwiz copy of the
# Boost Build system, including the bjam executable.  In particular, the script
#
# 1) Modifies the PATH environment variable so that the local pwiz bjam is
#    found first.
#
# 2) Sets the BOOST_BUILD_PATH environment variable to point to the local pwiz
#    Boost Build installation
#


if [ $BASH_SOURCE = $0 ]
then
    echo WARNING: You must use "'source $BASH_SOURCE'" to change the environment in
    echo the current shell.
    echo
    echo These changes to environment variables will not affect the current shell.
    echo
fi

# we expect this script to be located in pwiz root dir

PWIZ_ROOT=$(dirname $(readlink -f $BASH_SOURCE))

if [ ! -d $PWIZ_ROOT/libraries ]
then
    echo PWIZ_ROOT: $PWIZ_ROOT
    echo $PWIZ_ROOT/libraries not found.
    exit 1
fi

BOOST_BUILD_PATH_TEMP=$PWIZ_ROOT/libraries/boost-build
PWIZ_BJAM_PATH=$BOOST_BUILD_PATH_TEMP/engine/bin/$(uname -s)
PWIZ_BJAM=$PWIZ_BJAM_PATH/bjam

# sanity check: make sure the local pwiz bjam is where we think it is

if [ -f $PWIZ_BJAM ]
then
    echo bjam found:
    echo $PWIZ_BJAM
    echo
        
else
    echo bjam not found:
    echo $PWIZ_BJAM
    exit 1
fi

# set BOOST_BUILD_PATH to the local pwiz Boost Build dir
# set PATH so that local pwiz bjam is found first

export BOOST_BUILD_PATH=$BOOST_BUILD_PATH_TEMP
export PATH=$PWIZ_BJAM_PATH:$PATH

echo BOOST_BUILD_PATH:
echo $BOOST_BUILD_PATH
echo
echo PATH:
echo $PATH
echo

