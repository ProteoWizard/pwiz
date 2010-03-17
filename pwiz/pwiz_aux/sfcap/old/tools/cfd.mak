#
# cfd.mak
#

include project_paths.inc

TARGET = cfd.exe

SOURCES = \
  cfd.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = 
    
LIBS = pwiz_peaks pwiz_data pwiz_math

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

