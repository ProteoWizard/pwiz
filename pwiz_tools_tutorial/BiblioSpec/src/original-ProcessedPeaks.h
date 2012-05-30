//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


//Header file for ProcessedPeaks class

#ifndef PROCESSEDPEAKS_H
#define PROCESSEDPEAKS_H

#include <vector>
#include "original-Spectrum.h"

class ProcessedPeaks{
 public:
  vector<PEAK_T>* peaks;
  bool binned;
  bool processed;
  float totalIntensity;
  float precursor_mz;

  ProcessedPeaks();
  ProcessedPeaks(Spectrum* spec);
  ~ProcessedPeaks();
  //copy constructor
  //equal operator
};

#endif //PROCESSEDPEAKS_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
