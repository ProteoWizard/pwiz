if "%1" == "" GOTO SKIP_PUSH
pushd %1\libraries
:SKIP_PUSH

REM # test for last file extracted so we re-extract after a partial extraction
IF EXIST boost-build\site-config.jam GOTO SKIP_BB

echo Extracting Boost.Build...

REM # delete existing boost-build directory in case of partial extraction (because the last file extracted may be incomplete)
IF EXIST boost-build rmdir /s /q boost-build

bsdtar.exe -xkjf boost-build.tar.bz2

:SKIP_BB

copy /Y msvc.jam boost-build\tools
copy /Y testing.jam boost-build\tools

if "%1" == "" GOTO SKIP_POP
popd
:SKIP_POP