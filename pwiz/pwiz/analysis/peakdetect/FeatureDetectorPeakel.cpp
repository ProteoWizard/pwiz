//
// $Id$
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
#include "FeatureDetectorPeakel.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


using namespace pwiz::msdata;
using namespace std;
using boost::shared_ptr;


shared_ptr<FeatureDetectorPeakel> FeatureDetectorPeakel::create(FeatureDetectorPeakel::Config config)
{
    // note: config passed by value, allowing us to propagate the log pointers

    if (config.log)
    {
        config.peakFinder_SNR.log = config.log;
        config.peakelGrower_Proximity.log = config.log;
        config.peakelPicker_Basic.log = config.log;
    }

    shared_ptr<NoiseCalculator> noiseCalculator(
        new NoiseCalculator_2Pass(config.noiseCalculator_2Pass));

    shared_ptr<PeakFinder> peakFinder(new PeakFinder_SNR(noiseCalculator, config.peakFinder_SNR));
    shared_ptr<PeakFitter> peakFitter(new PeakFitter_Parabola(config.peakFitter_Parabola));
    shared_ptr<PeakExtractor> peakExtractor(new PeakExtractor(peakFinder, peakFitter));
    shared_ptr<PeakelGrower> peakelGrower(new PeakelGrower_Proximity(config.peakelGrower_Proximity));
    shared_ptr<PeakelPicker> peakelPicker(new PeakelPicker_Basic(config.peakelPicker_Basic));

    return shared_ptr<FeatureDetectorPeakel>(
        new FeatureDetectorPeakel(peakExtractor, peakelGrower, peakelPicker));
}


FeatureDetectorPeakel::FeatureDetectorPeakel(shared_ptr<PeakExtractor> peakExtractor,
                                             shared_ptr<PeakelGrower> peakelGrower,
                                             shared_ptr<PeakelPicker> peakelPicker)

:   peakExtractor_(peakExtractor),
    peakelGrower_(peakelGrower),
    peakelPicker_(peakelPicker)
{
    if (!peakExtractor.get() || !peakelGrower.get() || !peakelPicker.get()) 
        throw runtime_error("[FeatureDetectorPeakel] Null pointer");
}


namespace {

struct SetPeakMetadata
{
    const SpectrumInfo& spectrumInfo;

    SetPeakMetadata(const SpectrumInfo& _spectrumInfo) : spectrumInfo(_spectrumInfo) {}

    void operator()(Peak& peak) 
    {
        peak.id = spectrumInfo.scanNumber;
        peak.retentionTime = spectrumInfo.retentionTime;
    }
};


vector< vector<Peak> > extractPeaks(const MSData& msd, const PeakExtractor& peakExtractor)
{
    MSDataCache msdCache;
    msdCache.open(msd);

    const size_t spectrumCount = msdCache.size();
    vector< vector<Peak> > result(spectrumCount);

    for (size_t index=0; index<spectrumCount; index++)
    {
        const SpectrumInfo& spectrumInfo = msdCache.spectrumInfo(index, true);

        vector<Peak>& peaks = result[index];
        peakExtractor.extractPeaks(spectrumInfo.data, peaks);
        for_each(peaks.begin(), peaks.end(), SetPeakMetadata(spectrumInfo));

        /* TODO: logging
        if (os_)
        {
            *os_ << "index: " << index << endl;
            *os_ << "peaks: " << peaks.size() << endl; 
            copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        }
        */
    }

    return result;
}

} // namespace


void FeatureDetectorPeakel::detect(const MSData& msd, FeatureField& result) const
{
    vector< vector<Peak> > peaks = extractPeaks(msd, *peakExtractor_);

    PeakelField peakelField;
    peakelGrower_->sowPeaks(peakelField, peaks);

    peakelPicker_->pick(peakelField, result);
}


} // namespace analysis
} // namespace pwiz

