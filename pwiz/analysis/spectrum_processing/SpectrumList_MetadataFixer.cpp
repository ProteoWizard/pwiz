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

    BinaryData<double>& mzs = mzArray->data;
    BinaryData<double>& intensities = intensityArray->data;

    double tic = 0;
    if (!mzs.empty())
    {
        double bpmz, bpi = -1;
        for (size_t i=0, end=mzs.size(); i < end; ++i)
        {
            tic += intensities[i];
            if (bpi < intensities[i])
            {
                bpi = intensities[i];
                bpmz = mzs[i];
            }
        }

        replaceCvParam(*s, MS_base_peak_intensity, bpi, MS_number_of_detector_counts);
        replaceCvParam(*s, MS_base_peak_m_z, bpmz, MS_m_z);
        replaceCvParam(*s, MS_lowest_observed_m_z, mzs.front(), MS_m_z);
        replaceCvParam(*s, MS_highest_observed_m_z, mzs.back(), MS_m_z);
    }

    replaceCvParam(*s, MS_TIC, tic, MS_number_of_detector_counts);

    return s;
}


} // namespace analysis 
} // namespace pwiz
