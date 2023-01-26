/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#ifndef _CHARDKLOR2_H
#define _CHARDKLOR2_H

#include <iostream>
#include <string>
#include <vector>
#include <list>
#include <cmath>

#include "MSObject.h"
#include "MSReader.h"
#include "Spectrum.h"
#include "HardklorTypes.h"
#include "CAveragine.h"
#include "CMercury8.h"
#include "CHardklor.h"
#include "CModelLibrary.h"

#ifdef _MSC_VER
#include <Windows.h>
#include <profileapi.h>
#else
#include <sys/time.h>
#endif

class CHardklor2{

 public:
  //Constructors & Destructors:
	CHardklor2(CAveragine *a, CMercury8 *m, CModelLibrary *lib);
  ~CHardklor2();

  //Operators
  hkMem& operator[](const int& index);

  //Methods:
  void  Echo(bool b);
  int   GoHardklor(CHardklorSetting sett, MSToolkit::Spectrum* s=NULL);
  void  QuickCharge(MSToolkit::Spectrum& s, int index, std::vector<int>& v);
  void  SetResultsToMemory(bool b);
  int   Size();

 protected:

 private:
  //Methods:
  int     BinarySearch(MSToolkit::Spectrum& s, double mz, bool floor);
  double  CalcFWHM(double mz,double res,int iType);
  void    Centroid(MSToolkit::Spectrum& s, MSToolkit::Spectrum& out);
  bool    CheckForPeak(std::vector<Result>& vMR, MSToolkit::Spectrum& s, int index);
  int     CompareData(const void*, const void*);
  double  LinReg(std::vector<float>& mer, std::vector<float>& obs);
  bool    MatchSubSpectrum(MSToolkit::Spectrum& s, int peakIndex, pepHit& pep);
  double  PeakMatcher(std::vector<Result>& vMR, MSToolkit::Spectrum& s, double lower, double upper, double deltaM, int matchIndex, int& matchCount, int& indexOverlap, std::vector<int>& vMatchIndex, std::vector<float>& vMatchIntensity);
  double  PeakMatcherB(std::vector<Result>& vMR, MSToolkit::Spectrum& s, double lower, double upper, double deltaM, int matchIndex, int& matchCount, std::vector<int>& vMatchIndex, std::vector<float>& vMatchIntensity);
  void    QuickHardklor(MSToolkit::Spectrum& s, std::vector<pepHit>& vPeps);
  void    RefineHits(std::vector<pepHit>& vPeps, MSToolkit::Spectrum& s);
  void    ResultToMem(pepHit& ph, MSToolkit::Spectrum& s);
  void    WritePepLine(pepHit& ph, MSToolkit::Spectrum& s, FILE* fptr, int format=0); 
  void    WriteScanLine(MSToolkit::Spectrum& s, FILE* fptr, int format=0);
  void    SetIonFormula(pepHit& pep, const char* bestAveragine, double bestMass, double bestZeroMass, int bestCharge); // BSP edit, SKyline wants isotope envelope info


  static int CompareBPI(const void *p1, const void *p2);

  //Data Members:
  CHardklorSetting    cs;
  CAveragine*         averagine;
  CMercury8*          mercury;
  CModelLibrary*      models;
  CPeriodicTable*     PT;
  MSToolkit::Spectrum mask;
  hkMem               hkm;
  bool                bEcho;
  bool                bMem;
  int                 currentScanNumber;

  //Vector for holding results in memory should that be needed
  std::vector<hkMem> vResults;

  //Temporary Data Members:
  char bestCh[200];
  double BestCorr;
  int CorrMatches;
  int ExtraPre;
  int ExtraObs;

	int SSIterations;

  //For accurate timing of Hardklor
  #ifdef _MSC_VER
    __int64 startTime;
    __int64 stopTime;
    __int64 loadTime;
    __int64 analysisTime;
    __int64 timerFrequency;
    __int64 tmpTime1;
    __int64 tmpTime2;
    #define getExactTime(a) QueryPerformanceCounter((LARGE_INTEGER*)&a)
    #define getTimerFrequency(a) QueryPerformanceFrequency((LARGE_INTEGER*)&a)
    #define toMicroSec(a) (a)
    #define timeToSec(a,b) (a/b)
  #else
    timeval startTime;
    timeval stopTime;
    uint64_t loadTime;
    uint64_t splitTime;
    uint64_t analysisTime;
    uint64_t tmpTime1;
    uint64_t tmpTime2;
    int timerFrequency;
    #define getExactTime(a) gettimeofday(&a,NULL)
    #define toMicroSec(a) a.tv_sec*1000000+a.tv_usec
    #define getTimerFrequency(a) (a)=1
    #define timeToSec(a,b) (a/1000000)
  #endif

};

#endif
