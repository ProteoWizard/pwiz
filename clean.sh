#!/bin/bash

pwiz_root=$(dirname $0)
pushd $pwiz_root > /dev/null

echo "Cleaning project..."
if [ -d build-*-* ]; then rm -fdr build-*-*; fi;
if [ -d libraries/boost_1_36_0 ]; then rm -fdr libraries/boost_1_36_0; fi;
if [ -d libraries/boost_1_39_0 ]; then rm -fdr libraries/boost_1_39_0; fi;
if [ -d libraries/boost-build ]; then rm -fdr libraries/boost-build; fi;
if [ -d libraries/gd-2.0.33 ]; then rm -fdr libraries/gd-2.0.33; fi;
if [ -d libraries/zlib-1.2.3 ]; then rm -fdr libraries/zlib-1.2.3; fi;
if [ -d libraries/fftw-3.1.2 ]; then rm -fdr libraries/fftw-3.1.2; fi;
if [ -f libraries/libfftw3-3.def ]; then rm -f libraries/libfftw3-3.def; fi;
if [ -f libraries/libfftw3-3.dll ]; then rm -f libraries/libfftw3-3.dll; fi;
if [ -f pwiz/svnrev.hpp ]; then rm -f pwiz/svnrev.hpp; fi;
if [ -f pwiz/data/msdata/svnrev.hpp ]; then rm -f pwiz/data/msdata/svnrev.hpp; fi;
if [ -f pwiz/analysis/svnrev.hpp ]; then rm -f pwiz/analysis/svnrev.hpp; fi;
if [ -f pwiz/utility/proteome/svnrev.hpp ]; then rm -f pwiz/utility/proteome/svnrev.hpp; fi;
if [ -d pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data ]; then rm -fdr pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data; fi;
if [ -d pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data ]; then rm -fdr pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data; fi;
if [ -d pwiz_aux/msrc/data/vendor_readers/ABI/Reader_ABI_Test.data ]; then rm -fdr pwiz_aux/msrc/data/vendor_readers/ABI/Reader_ABI_Test.data; fi;
if [ -d pwiz_aux/msrc/data/vendor_readers/Waters/Reader_Waters_Test.data ]; then rm -fdr pwiz_aux/msrc/data/vendor_readers/Waters/Reader_Waters_Test.data; fi;
if [ -d pwiz_aux/msrc/data/vendor_readers/Bruker/Reader_Bruker_Test.data ]; then rm -fdr pwiz_aux/msrc/data/vendor_readers/Bruker/Reader_Bruker_Test.data; fi;

popd > /dev/null
