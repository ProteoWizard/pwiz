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
#ifndef _CHARDKLOR_H
#define _CHARDKLOR_H

#include <iostream>
#include <string>
#include <vector>

#include "HardklorTypes.h"
//#include "CHardklorProtein.h"
#include "CHardklorSetting.h"
#include "CHardklorVariant.h"
#include "CPeriodicTable.h"
#include "CSplitSpectrum.h"
#include "MSObject.h"
#include "MSReader.h"
#include "Spectrum.h"
#include "CNoiseReduction.h"
//#include "CHardklorFileReader.h"

#ifdef _MSC_VER
#include <Windows.h>
#include <profileapi.h>

#else
#include <sys/time.h>
#endif

#define PminusE 1.00672791

class SSObject {
public:
	//Data members
  std::vector<sInt> *pepVar;
  double corr;

	//Constructors & Destructors
  SSObject();
  SSObject(const SSObject& c);
	~SSObject();

	//Overloaded operators
  SSObject& operator=(const SSObject& c);

	//Functions
	void addVar(int a, int b);
	void clear();
  
};

class CHardklor{

 public:
  //Constructors & Destructors:
  CHardklor();
	CHardklor(CAveragine *a, CMercury8 *m);
  ~CHardklor();

  //Operators
  hkMem& operator[](const int& index);

  //Methods:
  void Echo(bool b);
  void ShowPerformanceHints(bool b);
  int GoHardklor(CHardklorSetting sett, MSToolkit::Spectrum* s=NULL);
	void SetAveragine(CAveragine *a);
	void SetMercury(CMercury8 *m);
  void SetResultsToMemory(bool b);
  int Size();

 protected:

 private:
  //Methods:
  void Analyze(MSToolkit::Spectrum* s=NULL);
  bool AnalyzePeaks(CSpecAnalyze& sa);
  int compareData(const void*, const void*);
  double LinReg(float *match, float *mismatch);
  void ResultToMem(SSObject& obj, CPeriodicTable* PT);
  void WriteParams(std::fstream& fptr, int format = 1);
  void WritePepLine(SSObject& obj, CPeriodicTable* PT, std::fstream& fptr, int format = 0);
  void WriteScanLine(MSToolkit::Spectrum& s, std::fstream& fptr, int format = 0);

	//Finished analysis algorithms
	void BasicMethod(float *match, float *mismatch,SSObject* combo, int depth, int maxDepth, int start);
	void DynamicMethod(float *match, float *mismatch,SSObject* combo, int depth, int maxDepth, int start, double corr);
	void DynamicSemiCompleteMethod(float *match, float *mismatch,SSObject* combo, int depth, int maxDepth, int start, double corr);
	void FewestPeptidesMethod(SSObject *combo, int maxDepth);
	void FewestPeptidesChoiceMethod(SSObject *combo, int maxDepth);
	void SemiCompleteMethod(float *match, float *mismatch,SSObject* combo, int depth, int maxDepth, int start);
	void SemiCompleteFastMethod(float *match, float *mismatch,SSObject* combo, int depth, int maxDepth, int start);
	void SemiSubtractiveMethod(SSObject *combo, int maxDepth);
	void FastFewestPeptidesMethod(SSObject *combo, int maxDepth);
	void FastFewestPeptidesChoiceMethod(SSObject *combo, int maxDepth);

	//Analysis algorithm support methods
	int calcDepth(int start, int max, int depth=1, int count=1);

  //Data Members:
	CSpecAnalyze sa;
	CHardklorSetting cs;
	CAveragine *averagine;
	CMercury8 *mercury;
	CPeriodicTable *PT;
  hkMem hkm;
	bool bEcho;
	bool bShowPerformanceHints;
  bool bMem;
  int currentScanNumber;
  std::fstream fptr; //TODO: Get rid of this and use FILE* instead.

	//Vector for holding peptide list of distribution
  std::vector<CHardklorVariant> pepVariants;

  //Vector for holding results in memory should that be needed
  std::vector<hkMem> vResults;

  //Temporary Data Members:
  char bestCh[200];
  double BestCorr;
  int CorrMatches;
  int ExtraPre;
  int ExtraObs;

  //For accurate timing of Hardklor
  #ifdef _MSC_VER
    __int64 startTime;
    __int64 stopTime;
    __int64 loadTime;
    __int64 splitTime;
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
