TARGET = EstimatePeakParameters.exe

SOURCES = \
	EstimatePeakParameters.cpp

OPTIONS = -O2 -I../../lib/boost_1_33_1 -I../peaks -DNDEBUG

ARCHIVES = peaks.a
    
LIBS = 

include ../make/standard.inc

