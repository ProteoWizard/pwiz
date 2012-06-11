#
# rtapprox.mak
#

include project_paths.inc

TARGET = rtapprox.exe

SOURCES = \
	FitFromPepXML.cpp \
	rtapprox.cpp

OPTIONS = -O2 -DNDEBUG -pedantic -Wall 

ARCHIVES = pepxml.a 
    
LIBS = xerces-c boost_program_options-gcc pwiz_math

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

$(BINDIR)/$(TARGET): $(OBJECTS) $(ARCHIVESFULLPATH)
	gcc -o $(basename $@) $(OBJECTS) $(LINKOPTIONS) $(ARCHIVESFULLPATH) $(addprefix -l, $(LIBS)) 
