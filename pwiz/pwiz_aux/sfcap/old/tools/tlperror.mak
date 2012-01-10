#
# tlperror.mak
#

include project_paths.inc

TARGET = tlperror.exe

SOURCES = \
	tlperror.cpp

OPTIONS = -O2 -pedantic -Wall -Werror -DNDEBUG

ARCHIVES = 
    
LIBS = pwiz_peaks pwiz_data pwiz_math

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

