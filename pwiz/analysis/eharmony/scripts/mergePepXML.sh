#!/bin/bash

file1=${1}
file2=${2}
outfile=${3}

sed  's/<\/msms_run_summary>//' $file1 > temp1

sed  's/<\/msms_pipeline_analysis>//' temp1 > temp2

awk 'BEGIN { DOPRINT = 0 } DOPRINT == 1 { print } /analysis_timestamp/ { DOPRINT = 1 }' ${file2} | tee -a temp2

mv temp2 $outfile

rm temp1
rm temp2