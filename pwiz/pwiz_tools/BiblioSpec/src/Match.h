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
 * $Id: Match.h, v 2.0 2008/11/20 09:53:52 Ning Zhang Exp $
 */
//header file for Match class

#ifndef MATCH_H
#define MATCH_H

#include <vector>
#include <iostream>
#include <iomanip>
#include <sstream>
#include <fstream>
#include "Spectrum.h"
#include "RefSpectrum.h"

namespace BiblioSpec {

enum SCORE_TYPE {DOTP,          ///< dot product
                 RAW_PVAL,      ///< q-value, no correction
                 BONF_PVAL,     ///< q-value, bonferroni corrected
                 QVAL,          ///< q-value
                 PEP,           ///< posterior error probability
                 MATCHED_IONS,  ///< number binned peaks shared

                 // always keep this one last as count
                 NUM_SCORE_TYPES};

class Match
{
 private:
  
  Spectrum* localSpec_;  
  RefSpectrum* localRef_;
  double scores_[NUM_SCORE_TYPES]; //scores for this match, indexed by type 

  int rank_;
  int matchLibID_;

 public:
  Match();
  Match(Spectrum* s, RefSpectrum* r);
  //Match(const Match& m);
  ~Match();

  //Match& operator= (const Match& m);
 
  // setters 
  static const char* getColumnHeaders();
  void setExpSpec(Spectrum* s);
  void setRefSpec(RefSpectrum* r);
  //void setProcessedPeaks(vector<Peak_T>* exp, vector<Peak_T>* ref);
  void setScore(SCORE_TYPE type, double score);
  void setRank(int rank);

  void setMatchLibID(int id); //remember the RefSpectrum from which Lib, mainly for decoy purpose
  
  // getters
  const Spectrum* getExpSpec() const;
  const RefSpectrum* getRefSpec() const;
  //vector<Peak_T>* getExpProcPeaks(); 
  //vector<Peak_T>* getRefProcPeaks();
  double getScore(SCORE_TYPE type) const;
  int getRank() const;
  int getMatchLibID() const;

};



 //comparing functions
  /*
  static bool compMatchDotScore(Match m1, Match m2);
  static bool compMatchRank(Match m1, Match m2);
  static bool compMatchPvalue(Match m1, Match m2);
  static bool compMatchQvalue(Match m1, Match m2);
  static bool compMatchPEPvalue(Match m1, Match m2);
  static bool compMatchScanNum(Match m1, Match m2);
  */

} // namespace

#endif //MATCH_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
