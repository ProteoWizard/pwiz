TARGET = peptideSieve.exe

SOURCES = \
 config.cpp \
 digest.cpp \
 classify.cpp \
 peptideSieve.cpp \



OPTIONS = -O2 -Wall -pedantic -Wno-uninitialized -Werror 

# -Wno-uninitialized is necessary to avoid spurious gcc warning


ARCHIVES = 

LIBS = boost_filesystem-gcc boost_program_options-gcc

OUTPUTDIR = ../../build/gcc

MSTOOLS_ROOT = ../..

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

