#
# 5pepfilt.mak
#

include project_paths.inc

TARGET = 5pepfilt.exe

SOURCES = \
	5pepfilt.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = 
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

