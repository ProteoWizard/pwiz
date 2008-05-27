//
// SpectrumList_NativeCentroider.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _SPECTRUMLIST_NATIVECENTROIDER_HPP_ 
#define _SPECTRUMLIST_NATIVECENTROIDER_HPP_ 


#include "utility/misc/Export.hpp"
#include "utility/misc/IntegerSet.hpp"
#include "SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation to return native centroided spectrum data
class PWIZ_API_DECL SpectrumList_NativeCentroider : public SpectrumListWrapper
{
    public:

    SpectrumList_NativeCentroider(const msdata::SpectrumListPtr& inner,
                                  const util::IntegerSet& msLevelsToCentroid);

    static bool accept(const msdata::SpectrumListPtr& inner);

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    private:
        const util::IntegerSet msLevelsToCentroid_;
        int mode_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_NATIVECENTROIDER_HPP_ 

