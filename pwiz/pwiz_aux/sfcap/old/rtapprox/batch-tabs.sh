#!/bin/bash
#
# batch-tabs.sh
#
# Note that you must have the build/gcc directory in your path in order 
# to have thisscript work properly.

SRC=$1
DEST=$2

for x in `find $SRC/* -type f`; do 
    OUTPUT=${DEST}/`basename ${x}`_result.txt
    tabapprox ${x} > ${OUTPUT}
done
