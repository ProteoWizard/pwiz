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

//class definition for Match

#include "Match.h"


namespace BiblioSpec {

Match::Match()
{
    localSpec_ = NULL;
    localRef_ = NULL;
    //expProcPeaks = NULL;
    //refProcPeaks = NULL;
 
    for(int i=0; i < NUM_SCORE_TYPES; i++){
        scores_[i] = -1;
    }
    rank_ = -1;
    matchLibID_ = -1;
}

Match::Match(Spectrum* s, RefSpectrum* ref)
{
    localSpec_ = s;
    localRef_ = ref;
    //expProcPeaks = NULL;
    //refProcPeaks = NULL;

    for(int i=0; i < NUM_SCORE_TYPES; i++){
        scores_[i] = -1;
    }
    rank_ = -1;
    matchLibID_ = -1;
}

/*
Match::Match(const Match& m)
{
    localSpec = m.localSpec;
    localRef = m.localRef;
    //expProcPeaks = m.expProcPeaks;
    //refProcPeaks = m.refProcPeaks;
  
    for(int i=0; i < NUM_SCORE_TYPES; i++){
        scores_[i] = m.scores_[i];
    }
    rank = m.rank;
    matchLibID = m.matchLibID;
}
*/
Match::~Match()
{
    //nothing new, nothing delete
}
/*
Match& Match::operator= (const Match& right)
{
    localSpec = right.localSpec;
    localRef = right.localRef;
    //expProcPeaks = right.expProcPeaks;
    //refProcPeaks = right.refProcPeaks;
  
    for(int i=0; i < NUM_SCORE_TYPES; i++){
        scores_[i] = right.scores_[i];
    }
    rank = right.rank;
    matchLibID = right.matchLibID;
    return *this;
}
*/
//setters
/*
void Match::setExpSpec(Spectrum* exp) {
    localSpec= exp;
    
}
void Match::setRefSpec(RefSpectrum* ref)
{
    localRef = ref;
}
*/
void Match::setScore(SCORE_TYPE type, double score){
    scores_[type] = score;
}
void Match::setRank(int zrank) {
    rank_ = zrank;
}
void Match::setMatchLibID(int id)
{
    matchLibID_ = id;
}

//getters
double Match::getScore(SCORE_TYPE type) const{
    double score = scores_[type];
    return score;
}
int Match::getRank() const{
    return rank_;
}
int Match::getMatchLibID() const
{
    return matchLibID_;
}
const Spectrum* Match::getExpSpec() const {
    return localSpec_;
}

const RefSpectrum* Match::getRefSpec() const
{
    return localRef_;
}
/*
  vector<Peak_T>* Match::getExpProcPeaks() {
  
  return expProcPeaks;
  
  }

  vector<Peak_T>* Match::getRefProcPeaks() {
 
  return refProcPeaks;
  }
*/

/*
  bool compMatchDotScore( Match m1, Match m2 )
  {  return m1.getDotProdScore() > m2.getDotProdScore(); }

  bool compMatchRank( Match m1, Match m2 )
  {  return m1.getRank() < m2.getRank(); }

  bool compMatchPvalue(Match m1, Match m2)
  {
  return m1.getLogPvalue() > m2.getLogPvalue();
  }

  bool compMatchQvalue(Match m1, Match m2)
  {
  return m1.getQvalue() > m2.getQvalue();
  }

  bool compMatchPEPvalue(Match m1, Match m2)
  {
  return m1.getProb() > m2.getProb();
  }

  bool compMatchScanNum(Match m1, Match m2)
  {
  return m1.getExpSpec()->getScanNumber() < m2.getExpSpec()->getScanNumber();
  }
*/

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
