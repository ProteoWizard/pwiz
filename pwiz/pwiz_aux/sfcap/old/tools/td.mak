#
# td.mak
#

include project_paths.inc

TARGET = td.exe

SOURCES = \
  td.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = 
    
LIBS = pwiz_peaks pwiz_data pwiz_math fftw3

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

