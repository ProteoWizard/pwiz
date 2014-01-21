#!/bin/bash

echo "Usage: ./make_input.sh path/to/results"

cat ${1}/tp.txt | awk '{print $5, 1}' | tee -a ${1}/result.txt
cat ${1}/fn.txt | awk '{print $5, 1}' | tee -a ${1}/result.txt
cat ${1}/fp.txt | awk '{print $5, 0}' | tee -a ${1}/result.txt
cat ${1}/tn.txt | awk '{print $5, 0}' | tee -a ${1}/result.txt