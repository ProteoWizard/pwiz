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
#include "MSReader.h"
#include "Spectrum.h"
#include "CHardklorSetting.h"
#include "CNoiseReduction.h"
#include <cmath>
#include <iostream>
#include <deque>

#define GC 5.5451774444795623

typedef struct cpar{
  int scan;
  double c;
} cpar;

class CHardklorFileReader {
public:
  CHardklorFileReader();
  CHardklorFileReader(CHardklorSetting& hs);
  ~CHardklorFileReader();

  int getPercent();
  //bool getProcessedSpectrum(Spectrum& s);
  //bool getProcessedSpectrumB(Spectrum& s);
  //bool initBuffers();
  //bool getNRSpectrum(Spectrum& s, int pos);

private:

  //Data Members
  int posNative;
  int posNR;
  CHardklorSetting cs;
  CNoiseReduction nr;
  MSToolkit::MSReader r;
  std::deque<MSToolkit::Spectrum> sNative;
  std::deque<MSToolkit::Spectrum> sNR;

  std::deque<cpar> dc;

  std::vector<int> vScans;
  
};