//
// $Id$
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


#ifndef _MS2DEISOTOPER_HPP_ 
#define _MS2DEISOTOPER_HPP_ 


#include "pwiz/analysis/common/DataFilter.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"

typedef struct {
    int charge;
    std::vector <int> indexList;
} isoChain;

namespace pwiz {
namespace analysis {


using chemistry::MZTolerance;


// Deisotope high resolution ms2 spectra

struct PWIZ_API_DECL MS2Deisotoper : public SpectrumDataFilter
{
     /// PrecursorMassFilter's parameters
    struct PWIZ_API_DECL Config
    {
        Config(MZTolerance tol = MZTolerance(0.5), bool hires_ = false, bool poisson_ = false, int maxCharge_ = 3, int minCharge_ = 1) 
            : matchingTolerance(tol), hires(hires_), poisson(poisson_), maxCharge(maxCharge_), minCharge(minCharge_) {}

        MZTolerance matchingTolerance;
        bool hires;
        bool poisson;
        int maxCharge;
        int minCharge;
    };

    MS2Deisotoper(const MS2Deisotoper::Config params_) : params(params_) {}
    virtual void operator () (const pwiz::msdata::SpectrumPtr&) const;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const;

    const MS2Deisotoper::Config params;
};


} // namespace analysis 
} // namespace pwiz


#endif // _MS2DEISOTOPER_HPP_ 
