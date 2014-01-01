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
                                                                                                     
#ifndef _PEAKELPICKER_HPP_
#define _PEAKELPICKER_HPP_


#include "PeakelPicker.hpp"
#include "MZRTField.hpp"
#include "pwiz/utility/misc/Export.hpp"


namespace pwiz {
namespace analysis {


///
/// interface for picking Peakels and arranging into Features;
///   note: Peakels are actually removed from the PeakelField
///
class PWIZ_API_DECL PeakelPicker
{
    public:

    virtual void pick(PeakelField& peakels, FeatureField& features) const = 0;

    virtual ~PeakelPicker(){}
};


///
/// basic implementation
///
class PWIZ_API_DECL PeakelPicker_Basic : public PeakelPicker
{
    public:

    struct Config
    {
        std::ostream* log;
        size_t minCharge;
        size_t maxCharge;
        size_t minMonoisotopicPeakelSize;
        MZTolerance mzTolerance;
        double rtTolerance;
        size_t minPeakelCount;

        //double intensityThreshold; // ? // some kind of tolerance to match isotope envelope
        //double relativeIntensityTolerance; 

        Config()
        :   log(0), 
            minCharge(2),
            maxCharge(5),
            minMonoisotopicPeakelSize(3),
            mzTolerance(10, MZTolerance::PPM),
            rtTolerance(5), // seconds
            minPeakelCount(3)
        {}
    };

    PeakelPicker_Basic(const Config& config = Config()) : config_(config) {}

    virtual void pick(PeakelField& peakels, FeatureField& features) const;

    private:
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKELPICKER_HPP_

