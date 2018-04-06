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


#ifndef _SPECTRUMLIST_MZWINDOW_HPP_
#define _SPECTRUMLIST_MZWINDOW_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList filter, for creating Spectrum sub-lists
class PWIZ_API_DECL SpectrumList_MZWindow : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_MZWindow(const msdata::SpectrumListPtr original, double mzLow, double mzHigh);

    /// \name SpectrumList interface
    //@{
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    double mzLow_;
    double mzHigh_;
    
    SpectrumList_MZWindow(SpectrumList_MZWindow&);
    SpectrumList_MZWindow& operator=(SpectrumList_MZWindow&);
};


} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_MZWINDOW_HPP_

