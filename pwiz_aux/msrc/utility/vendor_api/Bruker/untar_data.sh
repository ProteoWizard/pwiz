#!/bin/bash

pushd $1
if [ ! -e CompassDataTest.data ]; then
  tar -xkjvf CompassDataTest.data.tar.bz2
:SKIP
fi;
popd
