#
# peakext.mak
#

include project_paths.inc

TARGET = peakext.exe

SOURCES = \
  peakext.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = peaks.a data.a
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

