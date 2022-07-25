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
#include "pwiz/utility/misc/sort_together.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace util;


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

    auto mzBDA = spectrum->getMZArray();
    auto mz = mzBDA->data;

    vector<boost::iterator_range<BinaryData<double>::iterator>> equalLengthArrays;
    for (const auto& bda : spectrum->binaryDataArrayPtrs)
        if (bda != mzBDA && bda->data.size() == mz.size())
            equalLengthArrays.emplace_back(bda->data);
    sort_together(mz, equalLengthArrays);

    size_t begin = lower_bound(mz.cbegin(), mz.cend(), mzLow_) - mz.cbegin();
    size_t end = upper_bound(mz.cbegin(), mz.cend(), mzHigh_) - mz.cbegin();

    SpectrumPtr newSpectrum(new Spectrum(*spectrum));
    newSpectrum->binaryDataArrayPtrs.clear();

    BinaryDataArrayPtr newMZ(new BinaryDataArray);
    reinterpret_cast<ParamContainer&>(*newMZ) = reinterpret_cast<const ParamContainer&>(*mzBDA);
    newMZ->data.resize(end - begin);
    copy(mz.begin() + begin, mz.begin() + end, newMZ->data.begin());
    newSpectrum->binaryDataArrayPtrs.emplace_back(newMZ);
    newSpectrum->defaultArrayLength = newMZ->data.size();

    for (const auto& bda : spectrum->binaryDataArrayPtrs)
        if (bda != mzBDA && bda->data.size() == mz.size())
        {
            BinaryDataArrayPtr newBDA(new BinaryDataArray);
            reinterpret_cast<ParamContainer&>(*newBDA) = reinterpret_cast<const ParamContainer&>(*bda);

            newBDA->data.assign(bda->data.begin() + begin, bda->data.begin() + end);
            newSpectrum->binaryDataArrayPtrs.emplace_back(newBDA);
        }
    
    return newSpectrum;
}


} // namespace analysis
} // namespace pwiz


