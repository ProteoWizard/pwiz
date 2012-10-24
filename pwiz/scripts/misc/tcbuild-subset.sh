#!/bin/bash

function echo_info()
{
    echo "##teamcity[progressMessage '$*']"
}

function echo_error()
{
    echo "##teamcity[message text='$*' status='ERROR']"
}

# Make a clean subset working directory and move tarball there
rm -fr src_subset
mkdir -p src_subset
mv *-src-*.tar.bz2 src_subset
cd src_subset

# Extract subset tarball
tar -xf *-src-*.tar.bz2

echo_info "Cleaning project..."
if ! /bin/bash clean.sh; then
	  echo_error "Error cleaning project!"
	  exit 1 
fi

echo_info "Running quickbuild..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration "$@"; then
	  echo_error "Error running quickbuild! See full build log for details."
	  exit 1
fi

# uncomment this to test that test failures and error output are handled properly
# /bin/bash quickbuild.sh --teamcity-test-decoration pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest
