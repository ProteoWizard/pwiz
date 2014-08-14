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
echo "note automake version..."
automake --version
echo "note autoconf version..."
autoconf --version
echo "note libtoolize version..."
libtoolize --version
echo "note autoscan version..."
autoscan --version
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
echo "This is a semi-official ProteoWizard build system that uses GNU autotools instead of bjam. " > BUILDING 
echo "It leaves out support for vendor formats and MZ5 due to their extra build complexity. " >> BUILDING 
echo "If you need those, you'll want to learn the bjam system (and you'll need to be on windows, for vendor DLLs). " >> BUILDING 
echo "To use this GNU autotools build, from the pwiz directory just run:" >> BUILDING 
echo "   autotools/configure; make " >> BUILDING 
echo "Have a look at the autotools/README file if this does not go smoothly." >> BUILDING 
echo "Please visit http://proteowizard.sourceforge.net for the full official bjam build system, or to obtain updates to this GNU build system." >> BUILDING
echo "grab the boost autotools support stuff..."
# sigh... which version of wget is present?
wget -N http://github.com/tsuna/boost.m4/raw/master/build-aux/boost.m4 --no-check-certificate
if [ ! -e boost.m4 ]; then
wget -N http://github.com/tsuna/boost.m4/raw/master/build-aux/boost.m4
fi
echo "patch boost.m4 for 64bit RHEL"
sed -i 's#boost_$1$boost_tag_$boost_ver_#boost_$1$boost_tag_$boost_ver_ boost_$1#g' boost.m4
sed -i 's#"$with_boost" C:/Boost/lib /lib\*#"$with_boost" C:/Boost/lib /lib\* $with_boost/lib64#g' boost.m4

echo "do autoconf stuff..."
export PWIZ_VER_DOTTED=`grep "ProteoWizard " $TMPDIR/build.log | grep "last committed change" | cut -f 2 -d " "`
export PWIZ_VER=`echo $PWIZ_VER_DOTTED | sed s/\\\\./_/g`
# assume this as a directory where we can drop the tarball
# TODO this assumes a particular TC build config, could pull from log if needed
export TARBALLDIR=$PWIZROOT/build-linux-x86
mkdir -p $TARBALLDIR
# capturing stdout may interfere with VERSION file creation, make it now if needed
if [ ! -e $TARBALLDIR/VERSION ]; then
echo -n $PWIZ_VER_DOTTED > $TARBALLDIR/VERSION
fi
export TARBALL=$TARBALLDIR/libpwiz_src_$PWIZ_VER.tgz
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
echo "ls -l autotools"
ls -l autotools
echo "ls -l"
ls -l
if [ ! -e autotools/configure ] ; then
echo "something bad has happened: autotools/configure not found. Exit with error code 1."
exit 1
fi
echo "bash autotools/configure"
bash autotools/configure
echo "ls -l"
ls -l
echo "make check"
make check
rm -rf $TMPDIR

