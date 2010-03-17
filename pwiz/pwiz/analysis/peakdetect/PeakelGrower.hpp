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
                                                                                                     
#ifndef _PEAKELGROWER_HPP_
#define _PEAKELGROWER_HPP_


#include "MZTolerance.hpp"
#include "MZRTField.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include <set>


namespace pwiz {
namespace analysis {


///
/// interface for growing Peakels
///
class PWIZ_API_DECL PeakelGrower
{
    public:

    typedef pwiz::data::peakdata::Peak Peak;
    
    virtual void sowPeak(PeakelField& peakelField, const Peak& peak) const = 0;
    virtual void sowPeaks(PeakelField& peakelField, const std::vector<Peak>& peaks) const;
    virtual void sowPeaks(PeakelField& peakelField, const std::vector< std::vector<Peak> >& peaks) const;
    
    virtual ~PeakelGrower(){}
};


///
/// simple PeakelGrower implementation, based on proximity of Peaks
///
class PWIZ_API_DECL PeakelGrower_Proximity : public PeakelGrower
{
    public:
    
    struct Config
    {
        MZTolerance mzTolerance; // m/z units
        double rtTolerance; // seconds
        std::ostream* log;
        
        Config(double _mzTolerance = .01, double _rtTolerance = 10)
        :   mzTolerance(_mzTolerance), rtTolerance(_rtTolerance), log(0)
        {}
    };

    PeakelGrower_Proximity(const Config& config = Config());
    virtual void sowPeak(PeakelField&, const Peak& peak) const;

    private:
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKELGROWER_HPP_

