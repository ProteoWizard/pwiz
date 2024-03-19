//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_MetadataFixer.hpp"

#include "pwiz/utility/misc/almost_equal.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_MetadataFixer::SpectrumList_MetadataFixer(const msdata::SpectrumListPtr& inner)
:   SpectrumListWrapper(inner)
{
    // metadata fixing isn't really worth a processingMethod, is it?
}


PWIZ_API_DECL bool SpectrumList_MetadataFixer::accept(const msdata::SpectrumListPtr& inner)
{
    return true;
}


namespace {

template <typename value_type>
void replaceCvParam(ParamContainer& pc, CVID cvid, const value_type& value, CVID unit)
{
    vector<CVParam>::iterator itr;
    
    itr = std::find(pc.cvParams.begin(), pc.cvParams.end(), cvid);
    if (itr == pc.cvParams.end())
        pc.set(cvid, value, unit);
    else
        itr->value = lexical_cast<string>(value);
}

} // namespace


PWIZ_API_DECL SpectrumPtr SpectrumList_MetadataFixer::spectrum(size_t index, bool getBinaryData) const
{
    // always get binary data
    SpectrumPtr s = inner_->spectrum(index, true);

    BinaryDataArrayPtr mzArray = s->getMZArray();
    BinaryDataArrayPtr intensityArray = s->getIntensityArray();
    if (!mzArray.get() || !intensityArray.get())
        return s;

    const BinaryData<double>& mzs = mzArray->data;
    const BinaryData<double>& intensities = intensityArray->data;

    const auto metadata = calculatePeakMetadata(mzs, intensities);

    replaceCvParam(*s, MS_base_peak_intensity, metadata.basePeakY, MS_number_of_detector_counts);
    replaceCvParam(*s, MS_base_peak_m_z, metadata.basePeakX, MS_m_z);
    replaceCvParam(*s, MS_lowest_observed_m_z, metadata.lowestX, MS_m_z);
    replaceCvParam(*s, MS_highest_observed_m_z, metadata.highestX, MS_m_z);
    replaceCvParam(*s, MS_TIC, metadata.totalY, MS_number_of_detector_counts);

    return s;
}

template <typename XType, typename YType>
SpectrumList_MetadataFixer::PeakMetadata calculatePeakMetadata(const std::vector<XType>& xArray, const std::vector<YType>& yArray)
{
    SpectrumList_MetadataFixer::PeakMetadata result;
    if (!xArray.empty())
    {
        result.basePeakX = result.basePeakY = -1;
        result.lowestX = std::numeric_limits<double>::max();
        result.highestX = std::numeric_limits<double>::min();
        auto xItr = xArray.begin();
        auto yItr = yArray.begin();
        for (; xItr != xArray.end() && yItr != yArray.end(); ++xItr, ++yItr)
        {
            const auto y = static_cast<double>(*yItr);
            if (almost_equal(y, 0.0))
                continue;

            const auto x = static_cast<double>(*xItr);
            if (x > result.highestX)
                result.highestX = x;
            if (x < result.lowestX)
                result.lowestX = x;

            result.totalY += y;
            if (result.basePeakY < y)
            {
                result.basePeakY = y;
                result.basePeakX = x;
            }
        }
    }
    else
    {
        result.basePeakX = result.basePeakY = 0;
        result.lowestX = 0;
        result.highestX = 0;
    }

    return result;
}

SpectrumList_MetadataFixer::PeakMetadata SpectrumList_MetadataFixer::calculatePeakMetadata(const std::vector<float>& x, const std::vector<float>& y)
{ return pwiz::analysis::calculatePeakMetadata(x, y); }

SpectrumList_MetadataFixer::PeakMetadata SpectrumList_MetadataFixer::calculatePeakMetadata(const std::vector<double>& x, const std::vector<float>& y)
{ return pwiz::analysis::calculatePeakMetadata(x, y); }

SpectrumList_MetadataFixer::PeakMetadata SpectrumList_MetadataFixer::calculatePeakMetadata(const std::vector<double>& x, const std::vector<double>& y)
{ return pwiz::analysis::calculatePeakMetadata(x, y); }

} // namespace analysis 
} // namespace pwiz
