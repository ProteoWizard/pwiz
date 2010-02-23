#!/bin/bash

function echo_info()
{
    echo "##teamcity[progressMessage '$*']"
}

function echo_error()
{
    echo "##teamcity[message text='$*' status='ERROR']"
}


EXIT=0

echo_info "Cleaning project..."
if ! /bin/bash clean.sh; then
	  echo_error "Error cleaning project!"
	  exit 1 
fi

# the -p1 argument overrides bjam's default behavior of merging stderr into stdout

echo_info "Running quickbuild for release variant..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration -j4 release; then
	  echo_error "Error running quickbuild for release variant! See full build log for more details."
	  EXIT=1
fi

echo_info "Running quickbuild for debug variant..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration -j4 debug; then
	  echo_error "Error running quickbuild for debug variant! See full build log for more details."
	  EXIT=1
fi

echo_info "Running quickbuild for shared release variant..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration -j4 release link=shared; then
	  echo_error "Error running quickbuild for shared release variant! See full build log for more details."
	  EXIT=1
fi

echo_info "Running quickbuild for shared debug variant..."
if ! /bin/bash quickbuild.sh --teamcity-test-decoration -j4 debug link=shared; then
	  echo_error "Error running quickbuild for shared debug variant! See full build log for more details."
	  EXIT=1
fi

# uncomment this to test that test failures and error output are handled properly
# /bin/bash quickbuild.sh --teamcity-test-decoration pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest

exit $EXIT
