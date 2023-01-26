#Set these variables if needed
C = gcc
CC = g++
FLAGS = -O3 -static -D_NOSQLITE -D_LARGEFILE_SOURCE -D_FILE_OFFSET_BITS=64 -DGCC

#Path to MSToolkit

LIBPATH = ../MSToolkit
LIBS = -lmstoolkitlite

INCLUDE = -I$(LIBPATH)/include -I$(LIBPATH)/mzParser


#Do not touch these variables
SUPPORT = S2N.o Smooth.o FFT.o
HARDKLOR = CHardklor.o CAveragine.o CPeriodicTable.o CHardklorVariant.o CHardklorSetting.o CHardklorParser.o CNoiseReduction.o CSplitSpectrum.o CMercury8.o CSpecAnalyze.o SpecAnalyzeSupport.o FFT-HK.o CModelLibrary.o CHardklor2.o


#Make statements
hardklor : HardklorApp.cpp $(HARDKLOR) $(SUPPORT)
	$(CC) $(FLAGS) $(INCLUDE) $(SUPPORT) $(HARDKLOR) HardklorApp.cpp -L$(LIBPATH) $(LIBS) -o hardklor
	ar rcs libhardklor.a $(HARDKLOR) $(SUPPORT)

clean:
	rm -f *.o hardklor libhardklor.a


#Hardklor objects
S2N.o : S2N.cpp
	$(CC) $(FLAGS) $(INCLUDE)  S2N.cpp -c

Smooth.o : Smooth.cpp
	$(CC) $(FLAGS) $(INCLUDE) Smooth.cpp -c

FFT.o : FFT.cpp
	$(CC) $(FLAGS) $(INCLUDE) FFT.cpp -c

CHardklor.o : CHardklor.cpp
	$(CC) $(FLAGS) $(INCLUDE) CHardklor.cpp -c

CAveragine.o : CAveragine.cpp
	$(CC) $(FLAGS) $(INCLUDE) CAveragine.cpp -c

CPeriodicTable.o : CPeriodicTable.cpp
	$(CC) $(FLAGS) $(INCLUDE) CPeriodicTable.cpp -c

CHardklorVariant.o : CHardklorVariant.cpp
	$(CC) $(FLAGS) $(INCLUDE) CHardklorVariant.cpp -c

CHardklorSetting.o : CHardklorSetting.cpp
	$(CC) $(FLAGS) $(INCLUDE) CHardklorSetting.cpp -c

CHardklorParser.o : CHardklorParser.cpp
	$(CC) $(FLAGS) $(INCLUDE) CHardklorParser.cpp -c

CSplitSpectrum.o : CSplitSpectrum.cpp
	$(CC) $(FLAGS) $(INCLUDE) CSplitSpectrum.cpp -c

CMercury8.o : CMercury8.cpp
	$(CC) $(FLAGS) $(INCLUDE) CMercury8.cpp -c

CSpecAnalyze.o : CSpecAnalyze.cpp
	$(CC) $(FLAGS) $(INCLUDE) CSpecAnalyze.cpp -c
	
FFT-HK.o : FFT-HK.cpp
	$(CC) $(FLAGS) $(INCLUDE) FFT-HK.cpp -c

SpecAnalyzeSupport.o : SpecAnalyzeSupport.cpp
	$(CC) $(FLAGS) $(INCLUDE) SpecAnalyzeSupport.cpp -c

CNoiseReduction.o : CNoiseReduction.cpp
	$(CC) $(FLAGS) $(INCLUDE) CNoiseReduction.cpp -c

CModelLibrary.o : CModelLibrary.cpp
	$(CC) $(FLAGS) $(INCLUDE) CModelLibrary.cpp -c
	
CHardklor2.o : CHardklor2.cpp
	$(CC) $(FLAGS) $(INCLUDE) CHardklor2.cpp -c
