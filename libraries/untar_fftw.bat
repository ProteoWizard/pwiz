if "%1" == "" GOTO SKIP_PUSH
pushd %1\libraries
:SKIP_PUSH

IF EXIST fftw-3.1.2/api/fftw3.h GOTO SKIP_H
echo Extracting fftw header...
bsdtar.exe -xkjf fftw-3.1.2.tar.bz2 fftw-3.1.2/api/fftw3.h fftw-3.1.2/api/fftw3.f
:SKIP_H

IF EXIST libfftw3-3.dll GOTO SKIP_LIB
echo Extracting fftw lib...
bsdtar.exe -xkjf fftw-3.1-dll.tar.bz2 libfftw3-3.dll libfftw3-3.def
:SKIP_LIB

if "%1" == "" GOTO SKIP_POP
popd
:SKIP_POP
