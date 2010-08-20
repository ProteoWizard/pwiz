#
# error.mak
#

include project_paths.inc

TARGET = error.exe

SOURCES = \
  error.cpp

OPTIONS = -pedantic -Wall -Werror -O2

ARCHIVES =
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

