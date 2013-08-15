TARGET = midas2cfd.exe

SOURCES = \
  midas2cfd.cpp

OPTIONS = -O2 -I../../lib/fftw-3.1

#LINKOPTIONS = ../../lib/fftw-3.1/libfftw3-3.dll

ARCHIVES = peaks.a 

LIBS = fftw3

include ../make/executable.inc

