#!/bin/bash 
#
# makemake.sh uses autotools to create a GNU standard ("./configure ; make ; make install")
# build system for developers that just want to use pwiz without learning too much about it and bjam
# this is achieved by examining the results of a "bjam d+2" Proteowizard build.
#
# You can provide the build log as an argument, or this script will generate one for you.  It's only
# known to work with "./quickbuild.sh --without-binary-msdata -d+2". 
#
# note ProteoWizard sprinkles its header files throughout the code tree
# so we'll probably always do a full source install
#

# assume we're in ProteoWizard/pwiz/scripts/autotools
export HERE=`pwd`
pushd ../..
export PWIZROOT=`pwd`
export TMPDIR=`mktemp -d --tmpdir makemake.XXXXXX`
if [ $# -eq 0 ] ; then
./clean.sh
./quickbuild.sh --without-binary-msdata -d+2 > $TMPDIR/build.log
else
pushd $HERE
cp $1 $TMPDIR/build.log
popd
fi
echo "pre-clean..."
pushd pwiz
rm -f configure.scan autoscan*
popd
popd
pushd $TMPDIR
cp -f $PWIZROOT/LICENSE COPYING
cp -f $PWIZROOT/NOTICE README
cat FAQ >> README
echo "Please visit http://proteowizard.sourceforge.net/team.shtml for a list of authors" > AUTHORS
echo "Please visit http://proteowizard.sourceforge.net/news.shtml for ProteoWizard news" > NEWS
echo "Please visit http://proteowizard.svn.sourceforge.net/viewvc/proteowizard/trunk/pwiz/ for change histories" >ChangeLog 
echo "grab the boost autotools support stuff..."
wget -N http://github.com/tsuna/boost.m4/raw/master/build-aux/boost.m4
echo "autoconf..."
libtoolize --copy &>/dev/null; aclocal  &>/dev/null; autoscan $PWIZROOT/pwiz  &>/dev/null ; python $PWIZROOT/scripts/autotools/generate_autoconf.py $PWIZROOT $TMPDIR &>/dev/null
# yes, doing this twice, solves a chicken vs egg problem that first invocation barks about
libtoolize  --copy ; aclocal ; cat boost.m4 >> aclocal.m4 ; autoscan $PWIZROOT/pwiz ; python $PWIZROOT/scripts/autotools/generate_autoconf.py $PWIZROOT $TMPDIR

autoconf configure.ac > configure
chmod a+x configure
automake --add-missing  --copy

popd
#echo "now test our work..."
#pushd $PWIZROOT
#mkdir -p autotools
#cp -R $TMPDIR/* autotools
#autotools/configure
#make check
#popd
#rm -rf $TMPDIR

