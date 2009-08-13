if "%1" == "" GOTO SKIP_PUSH
pushd %1\libraries
:SKIP_PUSH

REM # test for last file extracted so we re-extract after a partial extraction
IF EXIST boost_1_39_0/tools/jam/test/var_expand.jam GOTO SKIP

echo Extracting boost tarball...

REM # delete existing boost directory in case of partial extraction (because the last file extracted may be incomplete)
IF EXIST boost_1_39_0 rmdir /s /q boost_1_39_0

REM # we only extract [chi]pp from boost_1_39_0/libs and [hi]pp from boost_1_39_0/boost
bsdtar.exe -xkjf boost_1_39_0.tar.bz2 boost_1_39_0/libs*.?pp boost_1_39_0/boost*.?pp boost_1_39_0/tools/jam boost_1_39_0/tools/build boost_1_39_0/libs/*timeconv.inl
:SKIP

if "%1" == "" GOTO SKIP_POP
popd
:SKIP_POP
