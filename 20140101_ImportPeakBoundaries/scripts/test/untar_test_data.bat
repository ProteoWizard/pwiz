@REM %1 = working directory containing the tarball
@REM %2 = pwiz root path
@REM %3 = name of untar'd result (adding .tar.bz2 must be the tarball filename)

pushd %1
IF EXIST %3 GOTO SKIP
%2\libraries\bsdtar.exe -xkjf %3.tar.bz2
:SKIP
popd