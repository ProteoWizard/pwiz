#
# pkscat.mak
#

include project_paths.inc

TARGET = pkscat.exe

SOURCES = \
  pkscat.cpp

OPTIONS = -pedantic -Wall -Werror -O2

ARCHIVES =
    
LIBS = pwiz_data pwiz_util boost_serialization-gcc

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

