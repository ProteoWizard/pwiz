TARGET = tau.exe

SOURCES = \
	tau.cpp

OPTIONS = -O2 -DNDEBUG

ARCHIVES = peaks.a
    
LIBS = 

include ../make/executable.inc

