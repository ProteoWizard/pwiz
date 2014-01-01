#!/bin/bash

echo "Usage: ./eharmonyAnalyser.sh original_ms2.pep.xml path/to/results"

./mergePepXML.sh $2{}/ms1_5.pep.xml ${1} ${2}/merged.pep.xml

#how many peptides did we identify with ms1.5
echo "Number of MS1.5 identifications: "
grep "<spectrum_query" ${2}/ms1_5.pep.xml | wc

#what were their sequences
echo "Writing unique MS1.5 sequences to file sequences.txt ..."
./PeptideProfile ${2}/ms1_5.pep.xml
cat profile.tsv | awk '{print $1}' > ${2}/sequences.txt

#how many of them were actually new 
./PeptideProfile ${1} 
cat profile.tsv | awk '{print $1}' > ${2}/temp.txt

echo "Writing diff between old sequences and new sequences to diff_sequences.txt ..."
python python/getListDifference.py ${2}/sequences.txt ${2}/temp.txt

# make a rocplot
### need to change make_input to have arg of path
echo "Calculating points for ROC curve ... "
./make_input.sh ${2}
./make_rocs.sh ${2}/result.txt
rm ${2}/result.txt

echo "Plotting ROC curve ... " 
echo "set term png" | tee -a gnufile
echo  "set output 'roc.png'" | tee -a gnufile
echo  "plot 'roc.txt' with lines ti '"${1}"'" | tee -a gnufile

gnuplot gnufile

mv roc.txt ${2}/roc.txt
mv roc.png ${2}/roc.png

mv list_diff.txt ${2}/diff_sequences.txt

rm gnufile
rm ${2}/temp.txt
