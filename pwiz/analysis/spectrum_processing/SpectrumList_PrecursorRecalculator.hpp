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


#ifndef _SPECTRUMLIST_PRECURSORRECALCULATOR_HPP_
#define _SPECTRUMLIST_PRECURSORRECALCULATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList wrapper that recalculates precursor info on spectrum() requests 
class PWIZ_API_DECL SpectrumList_PrecursorRecalculator : public msdata::SpectrumListWrapper
{
    public:

    /// constructor needs the full MSData object for instrument info;
    /// SpectrumList_PrecursorRecalculator holds a reference to the original SpectrumListPtr
    SpectrumList_PrecursorRecalculator(const msdata::MSData& msd);

    /// \name SpectrumList interface
    //@{
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    SpectrumList_PrecursorRecalculator(SpectrumList_PrecursorRecalculator&);
    SpectrumList_PrecursorRecalculator& operator=(SpectrumList_PrecursorRecalculator&);
};


} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_PRECURSORRECALCULATOR_HPP_

