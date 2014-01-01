#!/bin/bash

function echo_info()
{
    echo "##teamcity[progressMessage '$*']"
}

function echo_error()
{
    echo "##teamcity[message text='$*' status='ERROR']"
}

echo_info "Cleaning project..."
if ! /bin/bash clean.sh; then
	  echo_error "Error cleaning project!"
	  exit 1 
fi

echo_info "Running quickbuild..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration --verbose-test "$@"; then
	  echo_error "Error running quickbuild! See full build log for details."
	  exit 1
fi

# uncomment this to test that test failures and error output are handled properly
# /bin/bash quickbuild.sh --teamcity-test-decoration pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest
