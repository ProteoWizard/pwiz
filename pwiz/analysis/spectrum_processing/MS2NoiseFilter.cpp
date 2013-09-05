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


#include "MS2NoiseFilter.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {

using namespace std;
using namespace msdata;

const double factor = 0.5;

PWIZ_API_DECL MS2NoiseFilter::Config::Config(
            int           	numMassesInWindow_, 
            double          windowWidth_,
            bool            relaxLowMass_)
    :    
    numMassesInWindow(numMassesInWindow_), 
    windowWidth(windowWidth_),
    relaxLowMass(relaxLowMass_)
{
}


namespace {

struct indexValuePair
{
	double val;
	int	   index;
};

bool Greater ( indexValuePair elem1, indexValuePair elem2 )
{
   return elem1.val > elem2.val;
}

// used in binary transform below (nullable types?)
static double PropogateNulls(double& arg1, double& arg2)
{
    if (arg1 < 0) // indicates null
        return arg1;
    else
        return arg2;
}

struct FilterSpectrum
{
    FilterSpectrum( const MS2NoiseFilter::Config& params_, 
                    const pwiz::msdata::SpectrumPtr spectrum_);
    ~FilterSpectrum()
    {
    }

    void RetrievePrecursorMass();

    // data
    const MS2NoiseFilter::Config params;

    const pwiz::msdata::SpectrumPtr spectrum;
    std::vector<double>&            massList_;
    std::vector<double>&            intensities_;
    double                          precursorMZ;
    int                             precursorCharge;
};

void FilterSpectrum::RetrievePrecursorMass()
{
    BOOST_FOREACH(Precursor& precursor, spectrum->precursors)
    {
        BOOST_FOREACH(SelectedIon& selectedIon, precursor.selectedIons)
        {
            if (selectedIon.hasCVParam(MS_m_z))
            {
                precursorMZ = selectedIon.cvParam(MS_m_z).valueAs<double>();
            }
            else if (selectedIon.hasCVParam(MS_selected_ion_m_z))
            {
                precursorMZ = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
            }
            else
            {
                //TODO: log warning, unable to read precursor mz
                //cout << "unable to read precursor mz: " << spectrum->index << endl;
            }

            if (selectedIon.hasCVParam(MS_charge_state))
            {
                precursorCharge = selectedIon.cvParam(MS_charge_state).valueAs<int>();
            }
            else
            {
                //TODO: log warning, unable to read precursor charge state
                //cout << "unable to read precursor charge: " << spectrum->index << endl;
            }
        }
    }
}

FilterSpectrum::FilterSpectrum(const MS2NoiseFilter::Config& params_, 
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

    double windowWidth;

    if (params.windowWidth > 0.)
    {
        windowWidth = params.windowWidth;
    }
    else
    {
        if (spectrum->hasCVParam(MS_highest_observed_m_z))
        {
            windowWidth = spectrum->cvParam(MS_highest_observed_m_z).valueAs<double>();
        }
        else
        {
            windowWidth = 1000000; // use a token value in place of pre-recorded full mass range.
        }
    }

    RetrievePrecursorMass();

    if (precursorCharge > 0)
    {
        // remove any signal above the precursor minus mass of glycine
        //AminoAcid::Info::Record gly('G');
        double upperMassBound = precursorMZ * precursorCharge - 57.0214640;
        vector<double>::iterator lb = lower_bound(massList_.begin(), massList_.end(), upperMassBound);
        intensities_.erase(intensities_.begin() + (lb - massList_.begin()), intensities_.end());
        massList_.erase(lb, massList_.end());
    }

    // remove unfragmented precursor (within 0.5 Da)
    vector<double>::iterator ub = upper_bound(massList_.begin(), massList_.end(), precursorMZ + 0.5);
    vector<double>::iterator lb = lower_bound(massList_.begin(), massList_.end(), precursorMZ - 0.5);
    int ilb = lb - massList_.begin();
    int iub = ub - massList_.begin();
    intensities_.erase(intensities_.begin() + ilb, intensities_.begin() + iub);
    massList_.erase(lb, ub);

	lb = massList_.begin();
	ub = massList_.begin();

	while (ub != massList_.end())
	{
		ub = upper_bound(lb, massList_.end(), *lb + windowWidth);
		vector<double>::iterator it;
		vector<indexValuePair> indexValuePairs;

		for (it = lb; it<ub; it++)
		{
            if (intensities_[it - massList_.begin()] > 0)
            {
    			indexValuePair p;
    			p.index = it - massList_.begin();
    			p.val = intensities_[p.index];
    			indexValuePairs.push_back(p);
            }
		}

        size_t numMassesInWindow = params.numMassesInWindow;
        if (params.relaxLowMass && precursorCharge > 0)
        {
            if (min(precursorCharge, (int) ((precursorMZ * precursorCharge) / *lb)) > 1)
                numMassesInWindow = (size_t)(params.numMassesInWindow * (factor * min(precursorCharge, (int) ((precursorMZ * precursorCharge) / *lb))));
        }

        if (indexValuePairs.size() > numMassesInWindow)
        {
		    sort(indexValuePairs.begin(), indexValuePairs.end(), Greater);
            // Is there a more effective method for removing vector elements than this?
            for (size_t i = numMassesInWindow; i < indexValuePairs.size(); i++)
            {
                intensities_[indexValuePairs[i].index] = -1;
            }
        }
        do
        {
            lb++;
        }
        while (lb != massList_.end() && intensities_[lb-massList_.begin()] <= 0);
	}

    transform(intensities_.begin(), intensities_.end(), massList_.begin(), massList_.begin(), PropogateNulls);
    intensities_.erase(remove_if(intensities_.begin(), intensities_.end(), bind2nd(less<double>(), 0)), intensities_.end());
    massList_.erase(remove_if(massList_.begin(), massList_.end(), bind2nd(less<double>(), 0)), massList_.end());

    spectrum->defaultArrayLength = massList_.size();
}

} // namespace


PWIZ_API_DECL void MS2NoiseFilter::describe(ProcessingMethod& method) const
{
    method.set(MS_low_intensity_data_point_removal); // was MS_SpectrumFilter, now obsolete - MattC suggests this, which has def: "The removal of very low intensity data points that are likely to be spurious noise rather than real signal." 
    method.userParams.push_back(UserParam("num masses in window", lexical_cast<string>(params.numMassesInWindow)));
    method.userParams.push_back(UserParam("window width (Da)", lexical_cast<string>(params.windowWidth)));
    method.userParams.push_back(UserParam("allow more data below multiply charged precursor", lexical_cast<string>(params.relaxLowMass)));
}

PWIZ_API_DECL void MS2NoiseFilter::operator () (const SpectrumPtr spectrum) const
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
