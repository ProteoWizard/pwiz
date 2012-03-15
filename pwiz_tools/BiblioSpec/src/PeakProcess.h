/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
/*
 *  BiblioSpec Version 2.0
 *  Copyright 2008 University of Washington. All rights reserved.
 *  Written by Barbara Frewen, Michael J. MacCoss, William Stafford Noble
 *  in the Department of Genome Sciences at the University of Washington.
 *  http://proteome.gs.washington.edu/
 *
 *  $Id: PeakProcess.h, v 2.0 2008/11/20 09:53:52 Ning Zhang Exp $
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

using namespace std;
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
