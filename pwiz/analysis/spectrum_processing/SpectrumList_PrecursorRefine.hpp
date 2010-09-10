//
// $Id: SpectrumList_PrecursorRefine.hpp 1191 2009-08-14 19:33:05Z chambm $
//
//
// Original author: Chris Paulse <cpaulse@systemsbiology.org>
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


#ifndef _SPECTRUMLIST_PRECURSORREREFINE_HPP_
#define _SPECTRUMLIST_PRECURSORREREFINE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList wrapper that recalculates precursor info on spectrum() requests 
class PWIZ_API_DECL SpectrumList_PrecursorRefine : public msdata::SpectrumListWrapper
{
    public:

    struct PWIZ_API_DECL PrecursorInfo
    {
        double mz;
        double intensity;
        double charge;
        double score;

        PrecursorInfo() : mz(0), intensity(0), charge(0), score(0) {}
    };

    /// constructor needs the full MSData object for instrument info;
    /// SpectrumList_PrecursorRefine holds a reference to the original SpectrumListPtr
    SpectrumList_PrecursorRefine(const msdata::MSData& msd);
    ~SpectrumList_PrecursorRefine();

    /// \name SpectrumList interface
    //@{
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    double RefineMassVal(double initialEstimate, size_t index) const;
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    SpectrumList_PrecursorRefine(SpectrumList_PrecursorRefine&);
    SpectrumList_PrecursorRefine& operator=(SpectrumList_PrecursorRefine&);

mutable int numRefined;
mutable int numTotal;
};


} // namespace analysis
} // namespace pwiz


#endif // _SPECTRUMLIST_PRECURSORREREFINE_HPP_

