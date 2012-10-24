#
# midas2cfd.mak
#

include project_paths.inc

TARGET = midas2cfd.exe

SOURCES = \
  midas2cfd.cpp

OPTIONS = -O2 -I../../lib/fftw-3.1

ifeq ($(OS),Windows_NT)
LINKOPTIONS = ../../lib/fftw-3.1/libfftw3-3.dll
else
LIBS = fftw3
endif

ARCHIVES = data.a 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

