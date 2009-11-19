//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// PeptideMatcher.cpp
///

#include "PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include <map>
#include <iostream>

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::proteome;

struct Compare
{
    Compare(){}
    bool operator()(const boost::shared_ptr<SpectrumQuery>& a, const boost::shared_ptr<SpectrumQuery>& b)
    { 
        return (a->searchResult.searchHit.peptide < b->searchResult.searchHit.peptide); // sort vector of spectrum queries by sequence

    }

};

PeptideMatcher::PeptideMatcher(const PidfPtr _pidf_a, const PidfPtr _pidf_b)
{
    vector<boost::shared_ptr<SpectrumQuery> > a = _pidf_a->getAllContents();
    sort(a.begin(), a.end(), Compare());
    vector<boost::shared_ptr<SpectrumQuery> > b = _pidf_b->getAllContents();
    sort(b.begin(), b.end(), Compare());

    vector<boost::shared_ptr<SpectrumQuery> >::iterator a_it = a.begin();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator b_it = b.begin();

    while ( a_it != a.end() && b_it != b.end() )
        {
            if ((*a_it)->searchResult.searchHit.peptide == (*b_it)->searchResult.searchHit.peptide)
                {
                    if (fabs(Ion::mz((*a_it)->precursorNeutralMass, (*a_it)->assumedCharge) - Ion::mz((*b_it)->precursorNeutralMass, (*b_it)->assumedCharge)) < 1 ) _matches.push_back(make_pair(*a_it, *b_it));
                    ++a_it;
                    ++b_it;
                    
                }

            else if ((*a_it)->searchResult.searchHit.peptide > (*b_it)->searchResult.searchHit.peptide) ++b_it;
            else ++a_it;

        }



}

void PeptideMatcher::calculateDeltaRTDistribution()
{
    if (_matches.size() == 0)
        {
            cerr << "[PeptideMatcher::calculateDeltaRTDistribution] No matching MS/MS IDS found. DeltaRT params are both set to 0." << endl;
            return;
        }

    double meanSum = 0;
    PeptideMatchContainer::iterator match_it = _matches.begin();
    for(; match_it != _matches.end(); ++match_it)
        {           
            meanSum += (match_it->first->retentionTimeSec - match_it->second->retentionTimeSec);
           
        } 
    
    _meanDeltaRT = meanSum / _matches.size();

    double stdevSum = 0;
    PeptideMatchContainer::iterator stdev_it = _matches.begin();
    for(; stdev_it != _matches.end(); ++stdev_it)
        {   
            const double& rt_a = stdev_it->first->retentionTimeSec;
            const double& rt_b = stdev_it->second->retentionTimeSec;

            stdevSum += ((rt_a - rt_b) - _meanDeltaRT)*((rt_a - rt_b) - _meanDeltaRT);
         
        }

    _stdevDeltaRT = sqrt(stdevSum / _matches.size());
    return;

}

void PeptideMatcher::calculateDeltaMZDistribution()
{

  if (_matches.size() == 0)
    {
      cerr << "[PeptideMatcher::calculateDeltaMZDistribution] No matching MS/MS IDS found. DeltaMZ params are both set to 0." << endl;
      return;
    }

  double meanSum = 0;
  PeptideMatchContainer::iterator match_it = _matches.begin();
  for(; match_it != _matches.end(); ++match_it)
    {
      double increment = (Ion::mz(match_it->first->precursorNeutralMass, match_it->first->assumedCharge) - Ion::mz(match_it->second->precursorNeutralMass, match_it->second->assumedCharge));
      meanSum += increment;

    }

  _meanDeltaMZ = meanSum / _matches.size();

  double stdevSum = 0;
  PeptideMatchContainer::iterator stdev_it = _matches.begin();
  for(; stdev_it != _matches.end(); ++stdev_it)
    {
      const double& mz_a = Ion::mz(stdev_it->first->precursorNeutralMass, stdev_it->first->assumedCharge);
      const double& mz_b = Ion::mz(stdev_it->second->precursorNeutralMass, stdev_it->second->assumedCharge);

      stdevSum += ((mz_a - mz_b) - _meanDeltaMZ)*((mz_a - mz_b) - _meanDeltaMZ);

    }

  _stdevDeltaMZ = sqrt(stdevSum / _matches.size());
 

  return;

}

bool PeptideMatcher::operator==(const PeptideMatcher& that)
{
    return _matches == that.getMatches() &&
      make_pair(_meanDeltaRT, _stdevDeltaRT) == that.getDeltaRTParams();
    
}

bool PeptideMatcher::operator!=(const PeptideMatcher& that)
{
    return !(*this == that);

}
