#!/bin/bash
# $1 = working directory containing the tarball
# $2 = pwiz root path
# $3 = name of untar'd result (adding .tar.bz2 must be the tarball filename)

pushd $1
if [ ! -e $3 ]; then
  tar -xkjf $3.tar.bz2
fi;
popd
