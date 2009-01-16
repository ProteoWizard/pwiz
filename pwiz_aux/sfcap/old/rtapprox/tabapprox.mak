#
# tabapprox.mak
#

include project_paths.inc

TARGET = tabapprox.exe

SOURCES = \
	FitFromPepXML.cpp \
	tabapprox.cpp

OPTIONS = -O2 -DNDEBUG -pedantic -Wall 

ARCHIVES = pepxml.a 
    
LIBS = xerces-c pwiz_math

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

$(BINDIR)/$(TARGET): $(OBJECTS) $(ARCHIVESFULLPATH)
	gcc -o $(basename $@) $(OBJECTS) $(LINKOPTIONS) $(ARCHIVESFULLPATH) $(addprefix -l, $(LIBS)) 
