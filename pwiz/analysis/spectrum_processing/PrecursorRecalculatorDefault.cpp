//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "PrecursorRecalculatorDefault.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::msdata;
using namespace pwiz::data::peakdata;


//
// PrecursorRecalculatorDefault::Impl
//


class PrecursorRecalculatorDefault::Impl
{
    public:

    Impl(const Config& config)
    :   config_(config)
    {}

    void recalculate(const MZIntensityPair* begin,
                     const MZIntensityPair* end,
                     const PrecursorInfo& initialEstimate,
                     vector<PrecursorInfo>& result);

    private:
    Config config_;
};


namespace {

struct HasLowerMZ
{
    bool operator()(const MZIntensityPair& a, const MZIntensityPair& b){return a.mz < b.mz;}
};

PrecursorRecalculator::PrecursorInfo peakFamilyToPrecursorInfo(const PeakFamily& peakFamily)
{
    PrecursorRecalculator::PrecursorInfo result;
    result.mz = peakFamily.mzMonoisotopic;
    result.charge = peakFamily.charge;
    result.score = peakFamily.score;
    if (!peakFamily.peaks.empty())
        result.intensity = peakFamily.peaks[0].intensity;
    return result;
}

struct IsCloserTo
{
    IsCloserTo(double mz) : mz_(mz) {}

    bool operator()(const PrecursorRecalculator::PrecursorInfo& a, 
                    const PrecursorRecalculator::PrecursorInfo& b)
    {
        return fabs(a.mz - mz_) < fabs(b.mz - mz_);
    }

    private:
    double mz_;
};

} // namespace


void PrecursorRecalculatorDefault::Impl::recalculate(const MZIntensityPair* begin,
                                                     const MZIntensityPair* end,
                                                     const PrecursorInfo& initialEstimate,
                                                     vector<PrecursorInfo>& result)
{
    // use initial estimate to find window

    double mzLow = initialEstimate.mz - config_.mzLeftWidth;
    double mzHigh = initialEstimate.mz + config_.mzRightWidth;

    const MZIntensityPair* low = lower_bound(begin, end, MZIntensityPair(mzLow, 0), HasLowerMZ());
    const MZIntensityPair* high = lower_bound(begin, end, MZIntensityPair(mzHigh, 0), HasLowerMZ());

    // peak detection

    vector<PeakFamily> peakFamilies;
    config_.peakFamilyDetector->detect(low, high, peakFamilies);

    // translate PeakFamily result -> PrecursorInfo result

    transform(peakFamilies.begin(), peakFamilies.end(), back_inserter(result), peakFamilyToPrecursorInfo);

    // sort

    if (config_.sortBy == PrecursorRecalculatorDefault::Config::SortBy_Proximity)
        sort(result.begin(), result.end(), IsCloserTo(initialEstimate.mz));
    else
        throw runtime_error("[PrecursorRecalculatorDefault::recalculate()] sort not implemented");
}


//
// PrecursorRecalculatorDefault
//

PWIZ_API_DECL
PrecursorRecalculatorDefault::PrecursorRecalculatorDefault(const Config& config)
:   impl_(new Impl(config))
{}


PWIZ_API_DECL
void PrecursorRecalculatorDefault::recalculate(const MZIntensityPair* begin,
                                               const MZIntensityPair* end,
                                               const PrecursorInfo& initialEstimate,
                                               vector<PrecursorInfo>& result) const
{
    impl_->recalculate(begin, end, initialEstimate, result);
}


} // namespace analysis 
} // namespace pwiz


