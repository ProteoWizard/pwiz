# -- Author's Note ------------------------------------------------------------
#
# Well, it's not the worst Makefile I've ever written...
#
#
.DEFAULT_GOAL := all

C = gcc
CC = g++
CFLAGS = -O3 -static -I. -I./include -D_LARGEFILE_SOURCE -D_FILE_OFFSET_BITS=64 -DGCC -DHAVE_EXPAT_CONFIG_H
SFLAGS = -O3 -shared -fPIC -g -I. -I./include -D_LARGEFILE_SOURCE -D_FILE_OFFSET_BITS=64 -DGCC -DHAVE_EXPAT_CONFIG_H
LITEFLAGS = -D_NOSQLITE

SOVER = 83
RELVER = $(SOVER).0.1

SRC_DIR  := $(patsubst %/,%,$(dir $(abspath $(lastword $(MAKEFILE_LIST)))))
BUILD_SRC = $(SRC_DIR)/src
BUILD_DIR = $(SRC_DIR)/obj
BUILD_LIC = $(SRC_DIR)/lic

# Make build/staging directories. Should be used as a "order-only" prerequisite
# (prefixed with a "|" ) on any build rules.
$(BUILD_DIR)/ :
	mkdir -p $(BUILD_DIR)
	mkdir -p $(BUILD_LIC)
	
	

# -- Global build rules -------------------------------------------------------
#
# Rules for all packages of the MSToolkit.
#

.PHONY: all clean realclean
all       : expat zlib mstoolkit mzparser mzimltools sqlite lib MSSingleScan
clean     : expat-clean zlib-clean mstoolkit-clean mzparser-clean mzimltools-clean sqlite-clean lib-clean MSSingleScan-clean
realclean : expat-realclean zlib-realclean mstoolkit-realclean mzparser-realclean mzimltools-realclean sqlite-realclean lib-realclean MSSingleScan-realclean rc

# -- Expat XML Parser ---------------------------------------------------------
#
# Expat is an XML parser library written in C. It is a stream-oriented parser in
# which an application registers handlers for things the parser might find in
# the XML document (like start tags).  Released under the MIT license.
#
# http://expat.sourceforge.net/
#
EXPAT_VER := expat-2.2.9
EXPAT_SRC := $(BUILD_SRC)/$(EXPAT_VER)
EXPAT_DST := include/expat_config.h $(BUILD_DIR)/libexpat.a
EXPAT_DSO := $(BUILD_SRC)/$(EXPAT_VER)/lib/.libs/xmlparse.o $(BUILD_SRC)/$(EXPAT_VER)/lib/.libs/xmlrole.o $(BUILD_SRC)/$(EXPAT_VER)/lib/.libs/xmltok.o
EXPAT_LIC := $(BUILD_LIC)/$(EXPAT_VER)/COPYING

.PHONY : expat expat-clean expat-realclean

expat : $(EXPAT_DST) $(EXPAT_LIC)

$(EXPAT_DST) : | $(BUILD_DIR)/ $(EXPAT_SRC)/
	cd $(BUILD_SRC); tar -xzf $(EXPAT_VER).tar.gz
	cd $(EXPAT_SRC); ./configure --prefix=$(BUILD_DIR) \
	   --enable-shared=yes --enable-static=yes \
	   --without-xmlwf --without-examples --without-tests \
	   --with-aix-soname=both \
	   --includedir $(SRC_DIR)/include --libdir $(BUILD_DIR) 
	make -C $(EXPAT_SRC) install

$(EXPAT_LIC) : $(EXPAT_SRC)/$(notdir $(EXPAT_LIC))| $(MKDIR)
	mkdir -p $(BUILD_LIC)/$(EXPAT_VER)
	cp $^ $@

expat-clean :
ifneq (,$(wildcard $(EXPAT_SRC)/Makefile))
	make -C $(EXPAT_SRC) clean
endif

expat-realclean : expat-clean
	rm -rf $(EXPAT_LIC) $(EXPAT_DST) $(BUILD_DIR)/libexpat.la $(BUILD_DIR)/libexpat.so* $(BUILD_DIR)/bin $(BUILD_DIR)/man



# -- zlib compression library -------------------------------------------------
#
# A Massively Spiffy Yet Delicately Unobtrusive Compression Library
# (Also Free, Not to Mention Unencumbered by Patents)
# (Not Related to the Linux zlibc Compressing File-I/O Library)
#
# http://zlib.net/
#
ZLIB_VER := zlib-1.2.11
ZLIB_SRC := $(BUILD_SRC)/$(ZLIB_VER)
ZLIB_SRCB := $(wildcard $(ZLIB_SRC)/*.c)
ZLIB_DST := $(BUILD_DIR)/libz.a
ZLIB_LIC := $(BUILD_LIC)/$(ZLIB_VER)/README
ZLIB_DSO := $(patsubst ${ZLIB_SRC}/%.c, ${ZLIB_SRC}/%.lo, $(ZLIB_SRCB))

.PHONY : zlib zlib-clean zlib-realclean

zlib : $(ZLIB_DST) $(ZLIB_LIC)

$(ZLIB_DST) : | $(BUILD_DIR)/ $(ZLIB_SRC)/
	cd $(BUILD_SRC); unzip -o zlib1211.zip
	cd $(ZLIB_SRC); ./configure --prefix=$(BUILD_DIR) --includedir $(SRC_DIR)/include --libdir $(BUILD_DIR) 
	make -C $(ZLIB_SRC) install

$(ZLIB_LIC) : $(ZLIB_SRC)/$(notdir $(ZLIB_LIC))| $(MKDIR)
	mkdir -p $(BUILD_LIC)/$(ZLIB_VER)
	cp $^ $@

zlib-clean :
ifneq (,$(wildcard $(ZLIB_SRC)/Makefile))
	make -C $(ZLIB_SRC) clean
endif

zlib-realclean : zlib-clean
	rm -rf $(ZLIB_LIC) $(ZLIB_DST) $(BUILD_DIR)/libz.so*
	

# -- MSToolkit  ---------------------------------------------------------------
#
# The core MSToolkit files, but skip RAWReader on Linux systems
#
#
MSTOOLKIT_SRCDIR = $(BUILD_SRC)/MSToolkit/
MSTOOLKIT_DSTDIR = $(BUILD_DIR)/
MSTOOLKIT_SRC = $(filter-out $(BUILD_SRC)/MSToolkit/RAWReader.cpp, $(wildcard $(BUILD_SRC)/MSToolkit/*.cpp))
MSTOOLKIT_DST = $(patsubst ${MSTOOLKIT_SRCDIR}%.cpp, ${MSTOOLKIT_DSTDIR}%.o, $(MSTOOLKIT_SRC))
MSTOOLKIT_DSO = $(patsubst ${MSTOOLKIT_SRCDIR}%.cpp, ${MSTOOLKIT_DSTDIR}%.lo, $(MSTOOLKIT_SRC))
MSTOOLKIT_DSTLITE = $(patsubst ${MSTOOLKIT_SRCDIR}%.cpp, ${MSTOOLKIT_DSTDIR}%_lite.o, $(MSTOOLKIT_SRC))
MSTOOLKIT_DSOLITE = $(patsubst ${MSTOOLKIT_SRCDIR}%.cpp, ${MSTOOLKIT_DSTDIR}%_lite.lo, $(MSTOOLKIT_SRC))

.PHONY : mstoolkit

mstoolkit : $(MSTOOLKIT_DST) $(MSTOOLKIT_DSO) $(MSTOOLKIT_DSTLITE) $(MSTOOLKIT_DSOLITE)
	ar rcs $(BUILD_DIR)/libmst.a $(MSTOOLKIT_DST)
	ar rcs $(BUILD_DIR)/libmstlite.a $(MSTOOLKIT_DSTLITE)

$(MSTOOLKIT_DST) : | $(BUILD_DIR)/
$(MSTOOLKIT_DST) : $(MSTOOLKIT_DSTDIR)%.o : $(MSTOOLKIT_SRCDIR)%.cpp
	$(CC) $(CFLAGS) $< -c -o $@

$(MSTOOLKIT_DSO) : $(MSTOOLKIT_DSTDIR)%.lo : $(MSTOOLKIT_SRCDIR)%.cpp
	$(CC) $(SFLAGS) $< -c -o $@
	
$(MSTOOLKIT_DSTLITE) : $(MSTOOLKIT_DSTDIR)%_lite.o : $(MSTOOLKIT_SRCDIR)%.cpp
	$(CC) $(CFLAGS) $(LITEFLAGS) $< -c -o $@
	
$(MSTOOLKIT_DSOLITE) : $(MSTOOLKIT_DSTDIR)%_lite.lo : $(MSTOOLKIT_SRCDIR)%.cpp
	$(CC) $(SFLAGS) $(LITEFLAGS) $< -c -o $@
	
mstoolkit-clean :
	rm -rf $(MSTOOLKIT_DST) $(MSTOOLKIT_DSTLITE) $(MSTOOLKIT_DSO) $(MSTOOLKIT_DSOLITE)

mstoolkit-realclean : mstoolkit-clean
	rm -rf $(MSTOOLKIT_DST) $(MSTOOLKIT_DSTLITE) $(MSTOOLKIT_DSO) $(MSTOOLKIT_DSOLITE) $(BUILD_DIR)/libmst.a $(BUILD_DIR)/libmstlite.a
	

# -- mzParser  ---------------------------------------------------------------
#
# for reading the many TPP formats, with its own interface (and a RAMP interface)
#
#
MZPARSER_SRCDIR = $(BUILD_SRC)/mzParser/
MZPARSER_DSTDIR = $(BUILD_DIR)/
MZPARSER_SRC = $(filter-out $(BUILD_SRC)/mzParser/mzMLReader.cpp, $(wildcard $(BUILD_SRC)/mzParser/*.cpp))
MZPARSER_DST = $(patsubst ${MZPARSER_SRCDIR}%.cpp, ${MZPARSER_DSTDIR}%.o, $(MZPARSER_SRC))
MZPARSER_DSO = $(patsubst ${MZPARSER_SRCDIR}%.cpp, ${MZPARSER_DSTDIR}%.lo, $(MZPARSER_SRC))

.PHONY : mzparser

mzparser : $(MZPARSER_DST) $(MZPARSER_DSO)
	ar rcs $(BUILD_DIR)/libmzparser.a $(MZPARSER_DST)
	$(CC) $(SFLAGS) -o $(BUILD_DIR)/libmzparser.so.$(RELVER) -Wl,-z,relro -Wl,-soname,libmzparser.so.$(SOVER) $(MZPARSER_DSO)
	ln -sf $(BUILD_DIR)/libmzparser.so.$(RELVER) $(BUILD_DIR)/libmzparser.so.$(SOVER)
	ln -sf $(BUILD_DIR)/libmzparser.so.$(SOVER) $(BUILD_DIR)/libmzparser.so
	
$(MZPARSER_DST) : | $(BUILD_DIR)/
$(MZPARSER_DST) : $(MZPARSER_DSTDIR)%.o : $(MZPARSER_SRCDIR)%.cpp
	$(CC) $(CFLAGS) $< -c -o $@
	
$(MZPARSER_DSO) : $(MZPARSER_DSTDIR)%.lo : $(MZPARSER_SRCDIR)%.cpp
	$(CC) $(SFLAGS) $< -c -o $@
	
mzparser-clean :
	rm -rf $(MZPARSER_DST) $(MZPARSER_DSO)

mzparser-realclean : mzparser-clean
	rm -rf $(MZPARSER_DST) $(MZPARSER_DSO) $(BUILD_DIR)/libmzparser.a $(BUILD_DIR)/libmzparser.so*
	

# -- mzIMLTools  ---------------------------------------------------------------
#
# mzIdentMLTools is a c++ style interface for reading and writing mzID files
#
#
MZIMLTOOLS_SRCDIR = $(BUILD_SRC)/mzIMLTools/
MZIMLTOOLS_DSTDIR = $(BUILD_DIR)/
MZIMLTOOLS_SRC = $(wildcard $(BUILD_SRC)/mzIMLTools/*.cpp)
MZIMLTOOLS_DST = $(patsubst ${MZIMLTOOLS_SRCDIR}%.cpp, ${MZIMLTOOLS_DSTDIR}%.o, $(MZIMLTOOLS_SRC))
MZIMLTOOLS_DSO = $(patsubst ${MZIMLTOOLS_SRCDIR}%.cpp, ${MZIMLTOOLS_DSTDIR}%.lo, $(MZIMLTOOLS_SRC))

.PHONY : mzimltools

mzimltools : $(MZIMLTOOLS_DST) $(MZIMLTOOLS_DSO)
	ar rcs $(BUILD_DIR)/libmzimltools.a $(MZIMLTOOLS_DST)

$(MZIMLTOOLS_DST) : | $(BUILD_DIR)/
$(MZIMLTOOLS_DST) : $(MZIMLTOOLS_DSTDIR)%.o : $(MZIMLTOOLS_SRCDIR)%.cpp
	$(CC) $(CFLAGS) $< -c -o $@
	
$(MZIMLTOOLS_DSO) : $(MZIMLTOOLS_DSTDIR)%.lo : $(MZIMLTOOLS_SRCDIR)%.cpp
	$(CC) $(SFLAGS) $< -c -o $@
	
mzimltools-clean :
	rm -rf $(MZIMLTOOLS_DST) $(MZIMLTOOLS_DSO) 

mzimltools-realclean : mzimltools-clean
	rm -rf $(MZIMLTOOLS_DST) $(MZIMLTOOLS_DSO) $(BUILD_DIR)/libmzimltools.a
	

# -- SQLite  ---------------------------------------------------------------
#
# SQLite is a C-language library that implements a small, fast, 
# self-contained, high-reliability, full-featured, SQL database engine.
# It is public domain (no license), and I don't know if its features are
# still needed by the folks who asked for it in the MSToolkit.
#
# https://www.sqlite.org/
#
SQLITE_VER := sqlite-3.32.1
SQLITE_SRCDIR = $(BUILD_SRC)/$(SQLITE_VER)/
SQLITE_DSTDIR = $(BUILD_DIR)/
SQLITE_SRC = $(filter-out $(SQLITE_SRCDIR)shell.c, $(wildcard $(SQLITE_SRCDIR)*.c))
SQLITE_DST = $(patsubst $(SQLITE_SRCDIR)%.c, ${SQLITE_DSTDIR}%.o, $(SQLITE_SRC))
SQLITE_DSO = $(patsubst ${SQLITE_SRCDIR}%.c, ${SQLITE_DSTDIR}%.lo, $(SQLITE_SRC))

.PHONY : sqlite

sqlite : $(SQLITE_DST) $(SQLITE_DSO)
	ar rcs $(BUILD_DIR)/libsqlite.a $(SQLITE_DST)
	
$(SQLITE_DST) : | $(BUILD_DIR)/
$(SQLITE_DST) : $(SQLITE_DSTDIR)%.o : $(SQLITE_SRCDIR)%.c
	$(C) $(CFLAGS) $< -c -o $@
	
$(SQLITE_DSO) : $(SQLITE_DSTDIR)%.lo : $(SQLITE_SRCDIR)%.c
	$(C) $(SFLAGS) $< -c -o $@
	
sqlite-clean :
	rm -rf $(SQLITE_DST) $(SQLITE_DSO)

sqlite-realclean : sqlite-clean
	rm -rf $(SQLITE_DST) $(SQLITE_DSO) $(BUILD_DIR)/libsqlite.a
	

# -- Libraries  ----------------------------------------------------------
#
# The static libmstoolkit.a and shared libmstoolkit.so (I think)
# not sure how to link expat into the shared library.
#
#
.PHONY : lib

lib : expat zlib mstoolkit mzparser mzimltools sqlite
	ar -M <mstoolkit.mri
	ar -M <mstoolkitlite.mri
	$(CC) $(SFLAGS) -o libmstoolkit.so.$(RELVER) -Wl,-z,relro -Wl,-soname,libmstoolkit.so.$(SOVER) $(MSTOOLKIT_DSO) $(MZPARSER_DSO) $(MZIMLTOOLS_DSO) $(EXPAT_DSO) $(ZLIB_DSO) $(SQLITE_DSO) 
	ln -sf libmstoolkit.so.$(RELVER) libmstoolkit.so.$(SOVER)
	ln -sf libmstoolkit.so.$(SOVER) libmstoolkit.so
	$(CC) $(SFLAGS) -o libmstoolkitlite.so.$(RELVER) -Wl,-z,relro -Wl,-soname,libmstoolkitlite.so.$(SOVER) $(MSTOOLKIT_DSO) $(MZPARSER_DSO) $(MZIMLTOOLS_DSO) $(EXPAT_DSO) $(ZLIB_DSO)
	ln -sf libmstoolkitlite.so.$(RELVER) libmstoolkitlite.so.$(SOVER)
	ln -sf libmstoolkitlite.so.$(SOVER) libmstoolkitlite.so

lib-clean :
	rm -rf libmstoolkit.a libmstoolkitlite.a libmstoolkit.so* libmstoolkitlite.so*

lib-realclean : lib-clean
	rm -rf libmstoolkit.a libmstoolkitlite.a libmstoolkit.so* libmstoolkitlite.so*
	
	
# -- MSSingleScan  ----------------------------------------------------------
#
# Simple example program that utilizes the MSToolkit
#
#
.PHONY : MSSingleScan

MSSingleScan : lib
	$(CC) $(CFLAGS) $(SRC_DIR)/MSSingleScanSrc/MSSingleScan.cpp -L. -lmstoolkitlite -o MSSingleScan
	
MSSingleScan-clean :
	rm -rf MSSingleScan

MSSingleScan-realclean : lib-clean
	rm -rf MSSingleScan

	
# -- cleanup  ----------------------------------------------------------
#
#
.PHONY : rc

rc:
	rm -rf $(BUILD_DIR) $(BUILD_LIC)
