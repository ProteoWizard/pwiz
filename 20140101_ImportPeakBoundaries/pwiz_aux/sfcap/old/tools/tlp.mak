#
# tlp.mak
#

include project_paths.inc

TARGET = tlp.exe

SOURCES = \
  tlp.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = 
    
LIBS = pwiz_peaks pwiz_data pwiz_math

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

