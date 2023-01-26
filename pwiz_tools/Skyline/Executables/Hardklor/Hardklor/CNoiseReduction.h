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
#ifndef _CNOISEREDUCTION_H
#define _CNOISEREDUCTION_H

#include "MSReader.h"
#include "Spectrum.h"
#include "CHardklorSetting.h"
#include <cmath>
#include <iostream>
#include <deque>

#define GC 5.5451774444795623

class CNoiseReduction {

public:

  //Constructors and Destructors
  CNoiseReduction();
  CNoiseReduction(MSToolkit::MSReader* msr, CHardklorSetting& hs);
  ~CNoiseReduction();

  //Functions
  double CParam(MSToolkit::Spectrum& sp, int tot=1);
  double calcFWHM(double mz);
  void FirstDerivativePeaks(MSToolkit::Spectrum& sp, int winSize);
  bool DeNoise(MSToolkit::Spectrum& sp);
  //bool DeNoise(Spectrum& sp, vector<Spectrum>& vs, int pivot, bool findPeaks=false);
  //bool DeNoise(Spectrum& sp, deque<Spectrum>& vs, int pivot, bool findPeaks=false);
  bool DeNoiseB(MSToolkit::Spectrum& sp);
  bool DeNoiseC(MSToolkit::Spectrum& sp);
  bool DeNoiseD(MSToolkit::Spectrum& sp);
  int NearestPeak(MSToolkit::Spectrum& sp, double mz);
  bool ScanAverage(MSToolkit::Spectrum& sp, std::string, int width, float cutoff);
  bool NewScanAverage(MSToolkit::Spectrum& sp, std::string file, int width, float cutoff, int scanNum=0);
  //bool ScanAverage(Spectrum& sp, vector<Spectrum>& vs, int pivot, float cutoff, double cp=0.0);
  //bool ScanAverage(Spectrum& sp, deque<Spectrum>& vs, int pivot, float cutoff);
  //bool ScanAverageBuffered(Spectrum& sp, char* file, int width, float cutoff, int scanNum=0);
  bool ScanAveragePlusDeNoise(MSToolkit::Spectrum& sp, std::string file, int width, float cutoff, int scanNum=0);
  bool NewScanAveragePlusDeNoise(MSToolkit::Spectrum& sp, std::string file, int width, float cutoff, int scanNum=0);

  int pos;

private:
  //Functions
  
  
  //Data Members
  //int pos;
  int posA;
  std::string lastFile;
  CHardklorSetting cs;
  MSToolkit::MSReader* r;
  std::deque<MSToolkit::Spectrum> s;
  std::deque<MSToolkit::Spectrum> bs;

	/*
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
		*/

};

#endif

