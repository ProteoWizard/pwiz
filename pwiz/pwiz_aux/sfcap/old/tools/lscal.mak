#
# lscal.mak
#

include project_paths.inc

TARGET = lscal.exe

SOURCES = \
  lscal.cpp

OPTIONS = -pedantic -Wall -Werror -O2

ARCHIVES = calibration.a 
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

