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
#include "PeakFitter.hpp"
#include "pwiz/utility/math/Parabola.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::math;
using namespace std;


//
// PeakFitter
//


void PeakFitter::fitPeaks(const OrderedPairContainerRef& pairs,
                          vector<size_t>& indices,
                          vector<Peak>& result) const
{
    result.resize(indices.size());
    
    for (size_t i=0; i<indices.size(); i++)
        fitPeak(pairs, indices[i], result[i]); 
}


//
//
// PeakFitter_Parabola
//


PeakFitter_Parabola::PeakFitter_Parabola(const Config& config)
:   config_(config)
{}


namespace {
pair<double,double> OrderedPairToPair(const OrderedPair& p)
{
    return make_pair(p.x, p.y);
}
} // namespace


void PeakFitter_Parabola::fitPeak(const OrderedPairContainerRef& pairs,
                                  size_t index,
                                  Peak& result) const
{
    const OrderedPair* center = &pairs[index];
    const OrderedPair* begin = max(center-config_.windowRadius, pairs.begin());
    const OrderedPair* end = min(center+config_.windowRadius+1, pairs.end());

    if ((end-begin) < 3)
        throw runtime_error("[PeakFitter_Parabola] Not enough samples.");

    vector< pair<double,double> > samples;
    transform(begin, end, back_inserter(samples), OrderedPairToPair);

    Parabola p(samples);

    double totalIntensity = 0;
    double totalError2 = 0;

    for (const OrderedPair* it=begin; it!=end; ++it)
    {
        totalIntensity += it->y;
        double error = it->y - p(it->x);
        totalError2 += error*error;
    }

    result.mz = p.center();
    result.retentionTime = 0; 
    result.intensity = p(p.center());
    result.area = totalIntensity;
    result.error = sqrt(totalError2/(end-begin)); // rms error

    const size_t dataRadius = 3; 
    const OrderedPair* dataBegin = max(center-dataRadius, pairs.begin());
    const OrderedPair* dataEnd = min(center+dataRadius+1, pairs.end());
    result.data.clear();
    copy(dataBegin, dataEnd, back_inserter(result.data));
}


} // namespace analysis
} // namespace pwiz


