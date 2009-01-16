#
# tlpmatch.mak
#

include project_paths.inc

TARGET = tlpmatch.exe

SOURCES = \
	tlpmatch.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = peaks.a calibration.a extmath.a data.a
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

