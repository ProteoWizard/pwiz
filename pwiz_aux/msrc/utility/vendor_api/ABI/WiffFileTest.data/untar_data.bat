pushd %1
IF EXIST Enolase_repeats_AQv1.4.2.wiff GOTO SKIP
%2\libraries\bsdtar.exe -xkjvf Enolase_repeats_AQv1.4.2.tar.bz2
:SKIP
popd