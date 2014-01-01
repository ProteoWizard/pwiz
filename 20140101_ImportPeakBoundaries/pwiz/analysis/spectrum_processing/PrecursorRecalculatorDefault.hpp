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


#ifndef _PRECURSORRECALCULATORDEFAULT_HPP_ 
#define _PRECURSORRECALCULATORDEFAULT_HPP_ 


#include "PrecursorRecalculator.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetector.hpp"


namespace pwiz {
namespace analysis {


class PWIZ_API_DECL PrecursorRecalculatorDefault : public PrecursorRecalculator
{
    public:

    struct PWIZ_API_DECL Config
    {
        boost::shared_ptr<PeakFamilyDetector> peakFamilyDetector;
        std::ostream* log;

        double mzLeftWidth;
        double mzRightWidth;

        enum SortBy {SortBy_Proximity, SortBy_Score};
        SortBy sortBy;
        
        Config() : log(0), mzLeftWidth(0), mzRightWidth(0), sortBy(SortBy_Proximity) {}
    };

    PrecursorRecalculatorDefault(const Config&);

    virtual void recalculate(const MZIntensityPair* begin,
                             const MZIntensityPair* end,
                             const PrecursorInfo& initialEstimate,
                             std::vector<PrecursorInfo>& result) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    PrecursorRecalculatorDefault(PrecursorRecalculatorDefault&);
    PrecursorRecalculatorDefault& operator=(PrecursorRecalculatorDefault&);
};


} // namespace analysis 
} // namespace pwiz


#endif // _PRECURSORRECALCULATORDEFAULT_HPP_ 

