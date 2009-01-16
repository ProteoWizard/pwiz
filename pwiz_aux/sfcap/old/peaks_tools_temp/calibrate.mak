#
# calibrate.mak
#

include project_paths.inc

TARGET = calibrate.exe

SOURCES = \
  calibrate.cpp

OPTIONS = -O2

ARCHIVES = calibration.a proteome.a data.a
    
LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

