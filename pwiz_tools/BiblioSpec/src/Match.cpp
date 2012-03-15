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
//class definition for Match

#include "Match.h"

using namespace std;

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
