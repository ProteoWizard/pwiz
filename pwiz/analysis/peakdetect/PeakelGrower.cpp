//
// PeakelGrower.cpp
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
                                                                                                     
#define PWIZ_SOURCE
#include "PeakelGrower.hpp"


namespace pwiz {
namespace analysis {


using namespace std;
using namespace pwiz::data::peakdata;


//
// PeakelGrower
// 

void PeakelGrower::sowPeaks(PeakelField& peakelField, const vector<Peak>& peaks) const
{
    for (vector<Peak>::const_iterator it=peaks.begin(); it!= peaks.end(); ++it)
        sowPeak(peakelField, *it);
}


void PeakelGrower::sowPeaks(PeakelField& peakelField, const vector< vector<Peak> >& peaks) const
{
    for (vector< vector<Peak> >::const_iterator it=peaks.begin(); it!= peaks.end(); ++it)
        sowPeaks(peakelField, *it);
}


//
// PeakelGrower_Proximity
// 


PeakelGrower_Proximity::PeakelGrower_Proximity(const Config& config)
:   config_(config)
{}


namespace {

//#define PEAKELGROWER_DEBUG

bool isWithinTolerance(const Peakel& peakel, const Peak& peak, 
                       double toleranceRetentionTime)
{
    // assume Peakel::peaks vector is sorted by retentionTime 

    if (peakel.peaks.empty()) throw runtime_error("[PeakelGrower::isWithinTolerance()] This isn't happening.");

    double rtLow = peakel.peaks.front().retentionTime;
    double rtHigh = peakel.peaks.back().retentionTime;

    return (peak.retentionTime > rtLow - toleranceRetentionTime &&
            peak.retentionTime < rtHigh + toleranceRetentionTime);
}


vector<PeakelPtr> findCandidates(PeakelField& peakelField, const Peak& peak,
                                 double toleranceMZ, double toleranceRetentionTime)
{
    PeakelPtr target(new Peakel);

    target->mz = peak.mz - toleranceMZ;
    PeakelField::const_iterator begin = peakelField.lower_bound(target);

    target->mz = peak.mz + toleranceMZ;
    PeakelField::const_iterator end = peakelField.upper_bound(target);

    vector<PeakelPtr> result;
    for (PeakelField::const_iterator it=begin; it!=end; ++it)
        if (isWithinTolerance(**it, peak, toleranceRetentionTime))
            result.push_back(*it);

    return result;
}


void insertNewPeakel(PeakelField& peakelField, const Peak& peak)
{
    PeakelPtr peakel(new Peakel);
    peakel->mz = peak.mz;
    peakel->retentionTime = peak.retentionTime;
    peakel->peaks.push_back(peak);

    peakelField.insert(peakel);

#ifdef PEAKELGROWER_DEBUG
    cout << "insertNewPeakel():\n  " << *peakel << endl;
#endif
}


void updatePeakel(Peakel& peakel, const Peak& peak)
{
    peakel.peaks.push_back(peak);

#ifdef PEAKELGROWER_DEBUG
    cout << "updatePeakel():\n  " << peakel << endl;
#endif
}

} // namespace


void PeakelGrower_Proximity::sowPeak(PeakelField& peakelField, const Peak& peak) const
{
    vector<PeakelPtr> candidates = findCandidates(peakelField, peak, config_.toleranceMZ, config_.toleranceRetentionTime);

    if (candidates.empty())
        insertNewPeakel(peakelField, peak);
    else if (candidates.size() == 1)
        updatePeakel(*candidates.front(), peak);
    else
        throw runtime_error("[PeakelGrower_Proximity::sowPeak()] Multiple candidate peakels.");
}


} // namespace analysis
} // namespace pwiz


