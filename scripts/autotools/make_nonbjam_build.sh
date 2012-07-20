#!/bin/bash 
#
# uses autotools to create a GNU standard ("./configure ; make ; make install")
# build system for developers that just want to use pwiz without learning too much about it and bjam
# this is achieved by examining the results of a "bjam d+2" Proteowizard build.
#
# You can provide the build log as an argument, or this script will generate one for you.  It's only
# known to work with "./quickbuild.sh --without-binary-msdata -d+2". 
#
# note ProteoWizard sprinkles its header files throughout the code tree
# so we'll probably always do a full source install
#
#
# assume we're in ProteoWizard/pwiz/scripts/autotools
export HERE=`pwd`
pushd $HERE/../..
export PWIZROOT=`pwd`
export TMPDIR=`mktemp -d`
if [ $# -ne 1 ] ; then
# 0 or many args, assume we're under development (0) or in teamcity (many)
echo "running clean.sh"
./clean.sh
echo "running ./quickbuild.sh  --without-binary-msdata -d+2 "$@" | tee $TMPDIR/build.log"
./quickbuild.sh --without-binary-msdata -d+2 "$@" | tee $TMPDIR/build.log
else
echo "using existing logfile $1"
pushd $HERE
cp $1 $TMPDIR/build.log
popd
fi
echo "pre-clean autotools..."
pushd pwiz
rm -f configure.scan autoscan*
popd
popd
pushd $TMPDIR
cp -f $PWIZROOT/LICENSE COPYING
cp -f $PWIZROOT/NOTICE README
cat $HERE/FAQ >> README
echo "Please visit http://proteowizard.sourceforge.net/team.shtml for a list of authors" > AUTHORS
echo "Please visit http://proteowizard.sourceforge.net/news.shtml for ProteoWizard news" > NEWS
echo "Please visit http://proteowizard.svn.sourceforge.net/viewvc/proteowizard/trunk/pwiz/ for change histories" >ChangeLog 
echo "grab the boost autotools support stuff..."
# sigh... which version of wget is present?
wget -N http://github.com/tsuna/boost.m4/raw/master/build-aux/boost.m4 --no-check-certificate
echo $?
if [ ! -e boost.m4 ]; then
wget -N http://github.com/tsuna/boost.m4/raw/master/build-aux/boost.m4
fi
echo "do autoconf stuff..."
# assume this as a directory where we can drop the tarball
export TARBALLDIR=$PWIZROOT/build-linux-x86
mkdir -p $TARBALLDIR
export TARBALL=$TARBALLDIR/libpwiz_src.tgz
libtoolize --copy &>/dev/null; aclocal  &>/dev/null; autoscan $PWIZROOT/pwiz  &>/dev/null ; python $PWIZROOT/scripts/autotools/generate_autoconf.py $PWIZROOT $TMPDIR &>/dev/null
# yes, doing this twice, solves a chicken vs egg problem that first invocation barks about
libtoolize  --copy ; aclocal ; cat boost.m4 >> aclocal.m4 ; autoscan $PWIZROOT/pwiz ; python $PWIZROOT/scripts/autotools/generate_autoconf.py $PWIZROOT $TMPDIR $TARBALL

popd

echo "now test our work..."
cd $TARBALLDIR
rm -rf $TARBALLDIR/autotools_test
mkdir -p $TARBALLDIR/autotools_test
cd $TARBALLDIR/autotools_test
tar -xzf $TARBALL
cd pwiz
bash autotools/configure
make check
rm -rf $TMPDIR

