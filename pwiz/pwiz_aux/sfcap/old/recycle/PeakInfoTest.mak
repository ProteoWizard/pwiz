#
# PeakInfoTest.mak
#

include project_paths.inc

TARGET = PeakInfoTest.exe

SOURCES = \
  PeakInfo.cpp \
  PeakInfoTest.cpp

OPTIONS = -pedantic -Wall -Werror

ARCHIVES =
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

