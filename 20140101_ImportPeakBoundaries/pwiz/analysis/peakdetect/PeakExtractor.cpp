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
#include "PeakExtractor.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::math;


PeakExtractor::PeakExtractor(shared_ptr<PeakFinder> peakFinder,
                             shared_ptr<PeakFitter> peakFitter)
:   peakFinder_(peakFinder), peakFitter_(peakFitter)
{
    if (!peakFinder.get()) throw runtime_error("[PeakExtractor] Null PeakFinder.");
    if (!peakFitter.get()) throw runtime_error("[PeakExtractor] Null PeakFitter.");
}


void PeakExtractor::extractPeaks(const OrderedPairContainerRef& pairs,
                                 vector<Peak>& result) const
{
    result.clear();

    vector<size_t> peakIndices;
    peakFinder_->findPeaks(pairs, peakIndices);
    peakFitter_->fitPeaks(pairs, peakIndices, result);
}


} // namespace analysis
} // namespace pwiz


