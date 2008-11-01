pushd %1\libraries
IF EXIST fftw-3.1.2/api/fftw3.h GOTO SKIP
echo Extracting fftw header...
bsdtar.exe -xkzvf fftw-3.1.2.tar.gz fftw-3.1.2/api/fftw3.h fftw-3.1.2/api/fftw3.f
:SKIP
popd