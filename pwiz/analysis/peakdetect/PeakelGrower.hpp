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


#include "MZTolerance.hpp"
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
    typedef pwiz::data::peakdata::PeakelPtr PeakelPtr;

    bool operator()(const Peakel& a, const Peakel& b) const
    {
        if (a.mz < b.mz) return true;
        if (b.mz < a.mz) return false;
        return (a.retentionTime < b.retentionTime); // rare
    }

    bool operator()(const PeakelPtr& a, const PeakelPtr& b) const
    {
        return (*this)(*a, *b);
    }
};


///
/// PeakelField is a set of Peakels, stored as a binary tree ordered by LessThan_MZRT
///
struct PeakelField : public std::set<pwiz::data::peakdata::PeakelPtr, LessThan_MZRT>
{
    typedef pwiz::data::peakdata::PeakelPtr PeakelPtr;

    std::vector<PeakelPtr> find(double mz, MZTolerance mzTolerance,
                                double retentionTime, double rtTolerance) const;
};


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
        
        Config(double _mzTolerance = .1, double _rtTolerance = 10)
        :   mzTolerance(_mzTolerance), rtTolerance(_rtTolerance)
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

