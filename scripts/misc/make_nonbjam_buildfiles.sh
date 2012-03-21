#!/bin/bash

function echo_info()
{
    echo "##teamcity[progressMessage '$*']"
}

function echo_error()
{
    echo "##teamcity[message text='$*' status='ERROR']"
}

pushd pwiz/scripts/autotools
echo_info "Creating native autoconf/libtool build tools for libpwiz......"

if ! /bin/bash make_nonbjam_build.sh "$@"; then
	  echo_error "Error running make_nonbjam_build.sh! See full build log for details."
	  exit 1
fi


