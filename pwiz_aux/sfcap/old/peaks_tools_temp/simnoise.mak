#
# simnoise.mak
#

include project_paths.inc

TARGET = simnoise.exe

SOURCES = \
	simnoise.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = peaks.a extmath.a data.a
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

