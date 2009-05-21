//
// PeakExtractor.cpp
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
#include "PeakExtractor.hpp"


namespace pwiz {
namespace analysis {


using namespace std;
using namespace pwiz::math;
using boost::shared_ptr;


PeakExtractor::PeakExtractor(shared_ptr<PeakFinder> peakFinder,
                             shared_ptr<PeakFitter> peakFitter,
                             const Config& config)
:   peakFinder_(peakFinder), peakFitter_(peakFitter), config_(config)
{
    if (!peakFinder.get()) throw runtime_error("[PeakExtractor] Null PeakFinder.");
    if (!peakFitter.get()) throw runtime_error("[PeakExtractor] Null PeakFitter.");
}


namespace {
OrderedPair computeLogarithm(const OrderedPair& p)
{
    double value = p.y>0 ? log(p.y) : 0;
    return OrderedPair(p.x, value);
}
} // namespace


void PeakExtractor::extractPeaks(const OrderedPairContainerRef& pairs,
                                 vector<Peak>& result) const
{
    result.clear();

    vector<OrderedPair> processedData;
    if (config_.preprocessWithLogarithm)
        transform(pairs.begin(), pairs.end(), back_inserter(processedData), computeLogarithm);

    const OrderedPairContainerRef& data(config_.preprocessWithLogarithm ? processedData : pairs);

    vector<size_t> peakIndices;
    peakFinder_->findPeaks(data, peakIndices);

    peakFitter_->fitPeaks(pairs, peakIndices, result);
}


} // namespace analysis
} // namespace pwiz


