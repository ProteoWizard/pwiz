//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_3D_HPP_ 
#define _SPECTRUMLIST_3D_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include <boost/icl/interval_set.hpp>
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include <boost/container/flat_map.hpp>

namespace pwiz {
namespace analysis {

typedef boost::container::flat_map<double, boost::container::flat_map<double, float> > Spectrum3D;
typedef boost::shared_ptr<Spectrum3D> Spectrum3DPtr;

/// SpectrumList implementation that can create 3D spectra of ion mobility drift time and m/z
class PWIZ_API_DECL SpectrumList_3D : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_3D(const msdata::SpectrumListPtr& inner);

    static bool accept(const msdata::SpectrumListPtr& inner);
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    /// creates a 3d spectrum at the given scan start time (specified in seconds) and including the given drift time ranges (specified in milliseconds)
    virtual Spectrum3DPtr spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const;

    private:
    int mode_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_3D_HPP_ 
