//
// $Id: SpectrumList_PeakFilter.hpp 1191 2009-08-14 19:33:05Z chambm $
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


#ifndef _MS2_NOISE_FILTER_HPP_ 
#define _MS2_NOISE_FILTER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/analysis/common/DataFilter.hpp"


namespace pwiz {
namespace analysis {


// Noise peak removal in ms2 spectra.  We expect fragment masses to be relatively 
// equally spaced throughout the spectrum.  Select the top n most intense ions within a
// sliding window width of w.  Optionally, allow more peaks at lower mass (below precursor).
//
// Reference:
//   "When less can yield more – Computational preprocessing of MS/MS spectra for peptide
//    identification", Bernhard Y. Renard, Marc Kirchner, Flavio Monigatti, Alexander R. Ivanov,
//    Juri Rappsilber, Dominic Winter, Judith A. J. Steen, Fred A. Hamprecht and Hanno Steen
//    Proteomics, 9, 4978-4984, 2009.

struct PWIZ_API_DECL MS2NoiseFilter : public SpectrumDataFilter
{
    /// MS2NoiseFilter's parameters
    struct PWIZ_API_DECL Config
    {
        Config(
            int           	numMassesInWindow_ = 5, 
            double          windowWidth_ = 120.0,
            bool            relaxLowMass_ = false
			);

		size_t  numMassesInWindow;
		double  windowWidth;
        bool    relaxLowMass;
    };

    MS2NoiseFilter(const MS2NoiseFilter::Config params_) : params(params_) {}
    virtual void operator () (const pwiz::msdata::SpectrumPtr&) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const MS2NoiseFilter::Config params;
};


} // namespace analysis 
} // namespace pwiz


#endif // _MS2_NOISE_FILTER_HPP_ 
