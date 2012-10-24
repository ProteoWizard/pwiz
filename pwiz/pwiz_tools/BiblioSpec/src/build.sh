#!/bin/bash

# script for building BibloSpec tools
# to be run from proteowizard/pwiz_tools/BiblioSpec/src/

# get root of the proteowizard project to be two up from here
current_dir=`pwd`

PWIZ_ROOT=${current_dir%%pwiz_tools*}
if [ $current_dir == $PWIZ_ROOT ] ; then
  echo "Run from pwiz_tools/..."
  exit;
fi

export BOOST_BUILD_PATH=$PWIZ_ROOT/libraries/boost-build
PWIZ_BJAM_PATH=$BOOST_BUILD_PATH/engine/bin/$(uname -s)
PWIZ_BJAM=$PWIZ_BJAM_PATH/bjam

# build and pass any additional arguments to bjam
$PWIZ_BJAM --incremental -q toolset=gcc "$@"


