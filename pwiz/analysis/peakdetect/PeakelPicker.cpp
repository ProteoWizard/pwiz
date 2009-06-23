//
// PeakelPicker.cpp
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
#include "PeakelPicker.hpp"
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace pwiz::data::peakdata;
using namespace std;


namespace {


class BasicPickImpl
{
    public:

    BasicPickImpl(PeakelField& peakelField,
                  FeatureField& featureField,
                  const PeakelPicker_Basic::Config& config)
    :   peakelField_(peakelField), featureField_(featureField), config_(config)
    {}

    void pick();

    private:
    PeakelField& peakelField_;
    FeatureField& featureField_;
    const PeakelPicker_Basic::Config& config_;

    PeakelPtr getPeakelIsotope(const PeakelPtr& monoisotopicPeakel, size_t charge, size_t neutronNumber);
    void getFeatureCandidate(const PeakelPtr& peakel, size_t charge, vector<FeaturePtr>& result);
    FeaturePtr findFeature(const PeakelPtr& peakel);
    PeakelField::iterator removeFromPeakelField(const Feature& feature);
    PeakelField::iterator process(PeakelField::iterator it);
};


PeakelPtr BasicPickImpl::getPeakelIsotope(const PeakelPtr& monoisotopicPeakel,
                                          size_t charge, size_t neutronNumber)
{
    if (config_.log) *config_.log << "[PeakelPicker_Basic] getPeakelIsotope(): charge=" << charge 
        << " neutron=" << neutronNumber << endl;

    // find the peakel:
    //  - m/z must be within mzTolerance of the theoretical m/z
    //  - retentionTime must be within range (rtMin-rtTolerance, rtMax+rtTolerance)

    double mzTarget = monoisotopicPeakel->mz + 1./charge*neutronNumber;

    double rtTarget = (monoisotopicPeakel->retentionTimeMin() + 
        monoisotopicPeakel->retentionTimeMax()) / 2;

    double rtTolerance = (monoisotopicPeakel->retentionTimeMax() - 
        monoisotopicPeakel->retentionTimeMin()) / 2 + config_.rtTolerance;

    vector<PeakelPtr> isotopeCandidates = peakelField_.find(mzTarget, config_.mzTolerance,
                                                            rtTarget, rtTolerance);

    // TODO: restrict based on retentionTimeMax() of isotopeCandidates, since find()
    // uses retentionTimeMin() (via metadata)

    if (config_.log)
    {
        *config_.log << "[PeakelPicker_Basic] isotopeCandidates: " << isotopeCandidates.size() << endl;    
        for (vector<PeakelPtr>::const_iterator it=isotopeCandidates.begin(); it!=isotopeCandidates.end(); ++it)
            *config_.log << **it;
    }

    // if there are multiple candidates, may need to merge

    if (isotopeCandidates.empty())
        return PeakelPtr();
    else if (isotopeCandidates.size() == 1)
        return isotopeCandidates[0];
    else // TODO
    {
        cerr << "isotopeCandidates: " << isotopeCandidates.size() << endl;
        throw runtime_error("[PeakelPicker::getPeakelIsotope()] Multiple isotope candidates: not implemented.");
    }
}


void BasicPickImpl::getFeatureCandidate(const PeakelPtr& peakel, size_t charge, 
                                        vector<FeaturePtr>& result)
{
    if (config_.log) *config_.log << "[PeakelPicker_Basic] getFeatureCandidate(): z=" << charge << endl;

    FeaturePtr feature(new Feature);
    feature->peakels.push_back(peakel);

    const size_t maxNeutronNumber = 6;
    for (size_t neutronNumber=1; neutronNumber<=maxNeutronNumber; neutronNumber++)
    {
        PeakelPtr peakelIsotope = getPeakelIsotope(peakel, charge, neutronNumber);
        if (!peakelIsotope.get()) break;
        feature->peakels.push_back(peakelIsotope);
    }

    if (feature->peakels.size() >= config_.minPeakelCount)
    {
        // set feature basic metadata
        // can't do full recalculation until peakels removed from peakelField
    
        feature->mz = peakel->mz;
        feature->retentionTime = peakel->retentionTime;
        feature->charge = charge;
        result.push_back(feature);

        if (config_.log) *config_.log << "[PeakelPicker_Basic] Found feature candidate:\n" << *feature << endl;
    }
}


FeaturePtr BasicPickImpl::findFeature(const PeakelPtr& peakel)
{
    if (config_.log) *config_.log << "[PeakelPicker_Basic] findFeature():\n" << *peakel;

    vector<FeaturePtr> candidates;

    for (size_t z=config_.minCharge; z<=config_.maxCharge; z++)
        getFeatureCandidate(peakel, z, candidates); 
    
    if (candidates.empty())
        return FeaturePtr();
    else if (candidates.size() == 1)
        return candidates[0];
    else
    {
        cerr << "candidates: " << candidates.size() << endl;
        throw runtime_error("[PeakelPicker::findFeature()] Multiple candidates: not implemented.");
    }
}


PeakelField::iterator BasicPickImpl::removeFromPeakelField(const Feature& feature)
{
    if (feature.peakels.empty())
        throw runtime_error("[PeakelPicker::removeFromPeakelField()] Empty feature.");

    // remove feature's peakels from peakelField
    for (vector<PeakelPtr>::const_iterator it=feature.peakels.begin(); it!=feature.peakels.end(); ++it)
        peakelField_.remove(*it);

    // return the next valid iterator after the monoisotopic peakel
    return peakelField_.upper_bound(feature.peakels.front());
}


PeakelField::iterator BasicPickImpl::process(PeakelField::iterator it)
{
    FeaturePtr feature = findFeature(*it);

    if (feature.get())
    {
        if (config_.log) *config_.log << "[PeakelPicker_Basic] Feature found:\n" << *feature << endl;
        featureField_.insert(feature);
        return removeFromPeakelField(*feature);
    }
    else
    {
        return ++it;
    }
}


void BasicPickImpl::pick()
{
    if (config_.log) *config_.log << "[PeakelPicker_Basic] pick() begin\n\n" << peakelField_ << endl;

    PeakelField::iterator it = peakelField_.begin();
    PeakelField::iterator end = peakelField_.end();
   
    while (it != end)
        it = process(it);

    if (config_.log) *config_.log << "[PeakelPicker_Basic] pick() end\n\n";
}
    

} // namespace


void PeakelPicker_Basic::pick(PeakelField& peakels, FeatureField& features) const
{
    BasicPickImpl impl(peakels, features, config_);
    impl.pick();
}


} // namespace analysis
} // namespace pwiz

