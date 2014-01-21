#
# txt2cfd.mak
#

include project_paths.inc

TARGET = txt2cfd.exe

SOURCES = \
  txt2cfd.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = data.a
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

