//
// $Id: SpectrumList_PeakFilter.cpp 1191 2009-08-14 19:33:05Z chambm $
//
//
// Original author: Chris Paulse <cpaulse <a.t> systemsbiology.org>
//
// Copyright 2009 Institute for Systems Biology, Seattle, WA
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


#include "MS2Deisotoper.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {

using namespace std;
using namespace msdata;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;

using boost::shared_ptr;

namespace {

struct PWIZ_API_DECL FilterSpectrum
{
    FilterSpectrum(const MS2Deisotoper::Config& params_, 
                   const pwiz::msdata::SpectrumPtr spectrum_);
    ~FilterSpectrum()
    {
    }

    void DeIsotopeHiRes() { /* TODO: call peak family detector */ }
    void DeIsotopeLowRes();

    // data
    const MS2Deisotoper::Config params;

    const pwiz::msdata::SpectrumPtr spectrum;
    std::vector<double>&            massList_;
    std::vector<double>&            intensities_;
    double                          precursorMZ;
    int                             precursorCharge;
};

vector<pair<double, int> > GetPrecursors(const SpectrumPtr spectrum)
{
    vector<pair<double, int> > precursorList;

    BOOST_FOREACH(Precursor& precursor, spectrum->precursors)
    {
        BOOST_FOREACH(SelectedIon& selectedIon, precursor.selectedIons)
        {
            double mz = 0;
            int charge = 0;
            if (selectedIon.hasCVParam(MS_m_z))
            {
                mz = selectedIon.cvParam(MS_m_z).valueAs<double>();
            }
            else if (selectedIon.hasCVParam(MS_selected_ion_m_z))
            {
                mz = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            }

            if (selectedIon.hasCVParam(MS_charge_state))
            {
                charge = selectedIon.cvParam(MS_charge_state).valueAs<int>();
            }

            precursorList.push_back(pair<double, int>(mz, charge));
        }
    }

    return precursorList;
}

FilterSpectrum::FilterSpectrum(const MS2Deisotoper::Config& params_, 
                               const pwiz::msdata::SpectrumPtr spectrum_) 
                    : params(params_), 
                      spectrum(spectrum_), 
                      massList_(spectrum->getMZArray()->data), 
                      intensities_(spectrum->getIntensityArray()->data),
                      precursorMZ(0),
                      precursorCharge(0)
{
    if (massList_.size() < 1)
    {
        //TODO: log encounter with empty spectrum?
        return;
    }

    DeIsotopeLowRes();
}

static double PropogateNulls(double& arg1, double& arg2)
{
    if (arg1 < 0) // indicates null
        return arg1;
    else
        return arg2;
}

struct indexValuePair
{
	double val;
	size_t index;
};

// Override "less than" with "greater than" for stl::sort to output in descending order
bool operator < (const indexValuePair& lhs, const indexValuePair& rhs)
{
    return lhs.val > rhs.val;
}

void FilterSpectrum::DeIsotopeLowRes()
{
    vector<indexValuePair> indexValuePairs;

    size_t ix = 0;
    BOOST_FOREACH(double& intens, intensities_)
    {
		indexValuePair p;
		p.index = ix++;
        p.val = intens;
        indexValuePairs.push_back(p);
    }

    sort(indexValuePairs.begin(), indexValuePairs.end());
    
    int curIxValPair = 0;
    BOOST_FOREACH(indexValuePair& ix, indexValuePairs)
    {
        ++curIxValPair;

        if (intensities_[ix.index] >= 0)
        {
            if (params.hires)
            {
                size_t i = ix.index + 1;
                while (i < massList_.size() && massList_[i] - massList_[ix.index] < (2.0 + params.matchingTolerance.value))
                {
                    if (intensities_[i] < intensities_[ix.index])
                    {
                        intensities_[i] = -1.;
                    }
                    i++;
                }
            }
            else
            {
                for (size_t i = curIxValPair; i < indexValuePairs.size(); i++)
                {
                    double massDiff = massList_[indexValuePairs[i].index] - massList_[ix.index];
                    if (-massDiff < params.matchingTolerance.value && massDiff < (2.0 + params.matchingTolerance.value))
                    {
                        intensities_[indexValuePairs[i].index] = -1.;
                    }
                }
            }
        }
    }

    transform(intensities_.begin(), intensities_.end(), massList_.begin(), massList_.begin(), PropogateNulls);
    intensities_.erase(remove_if(intensities_.begin(), intensities_.end(), bind2nd(less<double>(), 0)), intensities_.end());
    massList_.erase(remove_if(massList_.begin(), massList_.end(), bind2nd(less<double>(), 0)), massList_.end());

    spectrum->defaultArrayLength = massList_.size();
}

} // namespace


void MS2Deisotoper::describe(ProcessingMethod& method) const
{
    //method.set(MS_ECD_ETD_Precursor_Mass_Filter);
    //method.userParams.push_back(UserParam("remove precursor", lexical_cast<string>(params.removePrecursor)));
    //method.userParams.push_back(UserParam("filter charge reduced precursors", lexical_cast<string>(params.removeReducedChargePrecursors)));
    //method.userParams.push_back(UserParam("remove neutral loss masses", lexical_cast<string>(params.neutralLossSpecies.size() > 0)));
    //method.userParams.push_back(UserParam("selective removal of precursors (most intense peak in tolerance window)", lexical_cast<string>(params.removeMostIntensePeakInWindow)));
    //method.userParams.push_back(UserParam("blanket removal of neutral loss masses", lexical_cast<string>(params.useBlanketFiltering)));
    //method.userParams.push_back(UserParam("matching tolerance", lexical_cast<string>(params.matchingTolerance)));
}

void MS2Deisotoper::operator () (const SpectrumPtr spectrum) const
{
    if (spectrum->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        spectrum->cvParam(MS_MSn_spectrum).empty() == false &&
        spectrum->precursors[0].empty() == false &&
        spectrum->precursors[0].selectedIons.empty() == false &&
        spectrum->precursors[0].selectedIons[0].empty() == false)
    {
        FilterSpectrum(params, spectrum);
    }
}

} // namespace analysis 
} // namespace pwiz
