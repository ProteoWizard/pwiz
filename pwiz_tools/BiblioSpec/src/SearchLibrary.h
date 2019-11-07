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
