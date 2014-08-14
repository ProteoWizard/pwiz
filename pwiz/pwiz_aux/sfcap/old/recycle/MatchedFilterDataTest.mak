#
# MatchedFilterDataTest.mak
#

include project_paths.inc

TARGET = MatchedFilterDataTest.exe

SOURCES = \
  MatchedFilterDataTest.cpp \
  MatchedFilterData.cpp

OPTIONS = -O2 -pedantic -Wall -Werror

ARCHIVES = extmath.a peaks.a data.a

LIBS = 

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

