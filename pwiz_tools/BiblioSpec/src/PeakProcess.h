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

/*
 *  $Id$
 *
 */

#ifndef PEAKPROCESS_H
#define PEAKPROCESS_H

#include <vector>
#include <numeric>
#include <string>
#include <sstream>
#include <cmath>
#include <algorithm>
#include "Spectrum.h"
#include "boost/program_options.hpp"

namespace ops = boost::program_options;

namespace BiblioSpec {
//enum FUN_TYPE { BIN, TOPN, NORMMZ};

class PeakProcessor
{
 private:
  bool isClearPrecursor_;
  bool noiseFirst_;  // remove noise peaks before normalizing intensities
  int numTopPeaks_;  //get signal peaks to match
  double binSize_;    // width of m/z bins for spectra
  double binOffset_;  // smallest value the of smallest bin

 public:
  PeakProcessor();
  PeakProcessor(const ops::variables_map& option);
  ~PeakProcessor();
  
  //getters and setters
  void setClearPrecursor(bool clear);
  void setNumTopPeaksToUse(int number);

  void processPeaks(Spectrum& s);
  void processPeaks(Spectrum* s);
  void removePrecursorPeaks(vector<PEAK_T>& peaks, double mz);

  double binPeaks(vector<PEAK_T>& peaks, vector<PEAK_T>& results);
  double getBin(double mz);
  double normMz(vector<PEAK_T>& peaks, vector<PEAK_T>& results, double denom); 
  double topNpeaks(vector<PEAK_T>& peaks, vector<PEAK_T>& results, double N);
  double quickTopNpeaks(vector<PEAK_T>& peaks, vector<PEAK_T>& results, int N);
  
  //comparing functions
  static bool compPeakMz(PEAK_T a, PEAK_T b);
  static bool compPeakIntDesc(PEAK_T a, PEAK_T b);
  static bool compPeakInt(PEAK_T a, PEAK_T b);

};

} // namespace

#endif //PEAKPROCESS_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
