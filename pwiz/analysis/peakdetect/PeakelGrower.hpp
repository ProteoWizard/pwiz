//
// PeakelGrower.hpp
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include <set>


namespace pwiz {
namespace analysis {


///
/// lexicographic ordering, by m/z then retention time
///
struct PWIZ_API_DECL LessThan_MZRT
{
    typedef pwiz::data::peakdata::Peakel Peakel;

    bool operator()(const Peakel& a, const Peakel& b)
    {
        const double epsilon = std::numeric_limits<double>::epsilon();
        if (a.mz < b.mz - epsilon) return true;
        if (b.mz < a.mz - epsilon) return false;
        if (a.retentionTime < b.retentionTime - epsilon) return true;
        return false;
    }
};


///
/// PeakelField is a set of Peakels, stored as a binary tree ordered by LessThan_MZRT
///
typedef std::set<pwiz::data::peakdata::Peakel, LessThan_MZRT> PeakelField;


///
/// interface for growing Peakels
///
class PWIZ_API_DECL PeakelGrower
{
    public:

    typedef pwiz::data::peakdata::Peak Peak;
    typedef pwiz::data::peakdata::Peakel Peakel;

    
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
        double toleranceMZ; // m/z units
        double toleranceRetentionTime; // seconds
        
        Config(double _toleranceMZ = .1, double _toleranceRetentionTime = 10)
        :   toleranceMZ(_toleranceMZ), toleranceRetentionTime(_toleranceRetentionTime)
        {}
    };

    PeakelGrower_Proximity(const Config& config = Config());

    private:
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKELGROWER_HPP_

