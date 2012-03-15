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
 *  BiblioSpec Version 1.0
 *  Copyright 2006 University of Washington. All rights reserved.  
 *  Written by Barbara Frewen, Michael J. MacCoss, William Stafford Noble
 *  in the Department of Genome Sciences at the University of Washington.  
 *  http://proteome.gs.washington.edu/
 *
 */

//Header for SearchLibrary, A class that will be the main program for searching a Spectrum against a library
#ifndef SEARCH_LIB_H
#define SEARCH_LIB_H

#include <iostream>
#include <sstream>
#include <fstream>
#include <vector>
#include <string>
#include <deque>
#include "DotProduct.h"
#include "Match.h"
#include "PeakProcess.h"
#include "Verbosity.h"
#include "LibReader.h"
#include "WeibullPvalue.h"
#include "Spectrum.h"
#include "boost/program_options.hpp"

using namespace std;
namespace ops = boost::program_options;

namespace BiblioSpec {

class SearchLibrary{

 public:
  const static int MIN_PEAK_SIZE = 5;

 private:
  PeakProcessor peakProcessor_;
  WeibullPvalue weibullEstimator_;
  double mzWindow_;
  int minPeaks_;
  int minSpecCharge_;
  int maxSpecCharge_;
  bool compute_pvalues_;
  int minWeibullScores_;  // generate decoys until this many scores per spec
  int decoysPerTarget_;
  double decoyMzShift_;
  bool shiftRawSpectra_;
  bool querySorted_;
  vector<LibReader*> libraries_;
  vector<Match> targetMatches_;          // target matches for a single spectrum
  vector<Match> decoyMatches_;           // decoy matches for a single spectrum
  deque<RefSpectrum*> cachedSpectra_;    // store spectra here for searching
  deque<RefSpectrum*> cachedDecoySpectra_;// store decoy spectra for searching
   
  ofstream weibullParamFile_;
  bool printAll_;

 public:
  
  SearchLibrary(vector<string>& libfilenames,
                const ops::variables_map& options_table);
  ~SearchLibrary();

  void searchSpectrum(BiblioSpec::Spectrum& querySpec);
  void getLibrarySpec(double minMz, double maxMz);
  void generateDecoySpectra(int startIdx);
  void runSearch(Spectrum& s);
  const vector<Match>& getTargetMatches();
  const vector<Match>& getDecoyMatches();

  // still needed by PSMfile
  float getShape();
  float getScale();
  float getFraction2Fit();
  void getWeibullHistogram(int hist[], int numElements);
  
 private:
  void initLibraries(Spectrum& spec);
  bool checkCharge(const vector<int>& queryCharges, int libCharge);
  void scoreMatches(Spectrum& s, deque<RefSpectrum*>& spectra, 
                    vector<Match>& matches);
  void setMatchesPvalues(int numScores);
  void updateSpectrumCache(double queryMz);
  void addNullScores(Spectrum s, vector<double>& scores);
  void setRank();

  // this should go in Match.h 
  static bool compMatchDotScore(Match m1, Match m2); // make these
                                                     // const refs

};

} // namespace

#endif //SEARCH_LIB_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
