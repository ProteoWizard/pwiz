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


echo_info "Copying *.tar.bz2 to Sourceforge..."
if ! scp -qB build/*.tar.bz2 proteowizard,proteowizard@frs.sourceforge.net:/home/frs/project/p/pr/proteowizard/proteowizard/; then
	echo_error "Error copying files to Sourceforge! See build output for full details."
fi

echo_info "Finished copying all tarballs to Sourceforge."
exit 0
