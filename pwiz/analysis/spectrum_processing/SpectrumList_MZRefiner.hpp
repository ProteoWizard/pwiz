//
// $Id$
//
//
// Original author: Bryson Gibbons <bryson.gibbons@pnnl.gov>
//
// Copyright 2014 Pacific Northwest National Laboratory
//                Richland, WA 99352
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


#ifndef _SPECTRUMLIST_MZREFINER_HPP_
#define _SPECTRUMLIST_MZREFINER_HPP_

/***************************************************************************************
****************************************************************************************
****************************************************************************************
****************************************************************************************
ASSUMPTIONS:
We are assuming that the instrument used to produce the data has at most ONE high-resolution mass analyzer.
- If an instrument contained TWO high-resolution mass analyzers, two different shifts would be needed, as well as the computations to create those shifts.
- We don't have such an instrument, and so we have no way to properly test that configuration, much less any reason to write code to work with it.
========================================================================================
LIMITATIONS:
This feature was created to modify m/z values using a bias generated from peptide identifications consistent with a single run.
- It has not been tested with merged data files. There would be little sense to using it with merged data files, since that would
     involve trying to create multiple shifts from one identification file and use them appropriately on one data file.
- It has not been tested on data files with combined spectra. I have not seen any reason to, and producing a good result would likely involve accessing the original spectra anyway.
- There are inherent challenges in finding the scan start time for a spectrum if it is not included in the identification file.
     If the identification file was not created using an mzML file (directly from native files), and the data file
        used for the refinement operation is not the mzML file (or the native file(s)), then we will probably just exclude
        the scan time dependent shift, since most other supported file types do not store the native ID.
========================================================================================
========================================================================================
DETERMINE IF THESE SHOULD BE ADDED TO THE USAGE INSTRUCTIONS
****************************************************************************************
****************************************************************************************
****************************************************************************************
***************************************************************************************/

#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"

namespace pwiz {
namespace analysis {

// SpectrumList wrapper that recalculates precursor info on spectrum() requests 
class PWIZ_API_DECL SpectrumList_MZRefiner : public msdata::SpectrumListWrapper
{
    public:
    SpectrumList_MZRefiner(const msdata::MSData& msd, const std::string& identFilePath, const std::string& cvTerm, const std::string& rangeSet, const util::IntegerSet& msLevelsToRefine, double step = 0.0, int maxStep = 0, bool assumeHighRes = false, pwiz::util::IterationListenerRegistry* ilr = NULL);

    /// \name SpectrumList interface
    //@{
    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
};

} // namespace analysis
} // namespace pwiz

#endif // _SPECTRUMLIST_MZREFINER_HPP_
