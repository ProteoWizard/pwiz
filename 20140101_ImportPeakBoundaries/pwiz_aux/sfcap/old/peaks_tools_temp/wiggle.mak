TARGET = wiggle.exe

SOURCES = \
	wiggle.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = peaks.a
    
LIBS = 

include ../make/executable.inc

