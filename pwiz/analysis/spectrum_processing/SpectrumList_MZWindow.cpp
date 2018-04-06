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

#define PWIZ_SOURCE


#include "SpectrumList_MZWindow.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;


PWIZ_API_DECL
SpectrumList_MZWindow::SpectrumList_MZWindow(const SpectrumListPtr original, double mzLow, double mzHigh)
:   SpectrumListWrapper(original), mzLow_(mzLow), mzHigh_(mzHigh)
{}


namespace {
inline bool hasLowerMZ(const MZIntensityPair& a, const MZIntensityPair& b) {return a.mz < b.mz;}
} // namespace


PWIZ_API_DECL
msdata::SpectrumPtr SpectrumList_MZWindow::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr spectrum = inner_->spectrum(index, getBinaryData);
    if (!getBinaryData) return spectrum;

    vector<MZIntensityPair> data;
    spectrum->getMZIntensityPairs(data);

    vector<MZIntensityPair>::const_iterator begin = lower_bound(
        data.begin(), data.end(), MZIntensityPair(mzLow_,0), hasLowerMZ);

    vector<MZIntensityPair>::const_iterator end = upper_bound(
        data.begin(), data.end(), MZIntensityPair(mzHigh_,0), hasLowerMZ);

    vector<MZIntensityPair> newData;
    copy(begin, end, back_inserter(newData));

    BinaryDataArrayPtr intensityArray = spectrum->getIntensityArray();
    CVID intensityUnits = intensityArray.get() ? intensityArray->cvParam(MS_intensity_unit).cvid : CVID_Unknown;
    
    SpectrumPtr newSpectrum(new Spectrum(*spectrum));
    newSpectrum->binaryDataArrayPtrs.clear();
    newSpectrum->setMZIntensityPairs(newData, intensityUnits);

    return newSpectrum;
}


} // namespace analysis
} // namespace pwiz


