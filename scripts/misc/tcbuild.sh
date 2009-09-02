#!/bin/bash

function echo_info()
{
    echo "##teamcity[message text='$*']"
    echo "##teamcity[progressMessage '$*']"
}

function echo_error()
{
    echo "##teamcity[message text='$*' status='ERROR']"
    exit 1
}

echo_info "Cleaning project..."
if ! sh clean.sh
then
	echo_error "Error cleaning project!"
fi

echo_info "Running quickbuild.sh..."
if ! sh quickbuild.sh ci=teamcity $1
then
	echo "Error running quickbuild!"
	echo_error "Error running quickbuild! See full build log for more details."
fi

exit 0
