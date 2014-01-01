TARGET = tltaylor.exe

SOURCES = \
	tltaylor.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = peaks.a extmath.a
    
LIBS = 

include ../make/executable.inc

