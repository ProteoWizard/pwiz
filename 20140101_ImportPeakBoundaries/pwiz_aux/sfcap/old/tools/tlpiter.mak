#
# tlpiter.mak
#

include project_paths.inc

TARGET = tlpiter.exe

SOURCES = \
	tlpiter.cpp

OPTIONS = -O2 -pedantic -Wall -Werror -DNDEBUG

ARCHIVES =
    
LIBS = pwiz_peaks pwiz_data pwiz_math 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

