@REM %1 = working directory containing the tarball
@REM %2 = pwiz root path
@REM %3 = name of untar'd result (adding .tar.bz2 must be the tarball filename)
@REM %4 = extra args (e.g. --exclude "*.mzML")

pushd %1
%2\libraries\bsdtar.exe %4 -cjf %3.tar.bz2 %3
popd