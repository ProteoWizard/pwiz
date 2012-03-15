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
