//
// SpectrumList_SavitzkyGolaySmoother.hpp
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


#ifndef _SPECTRUMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_
#define _SPECTRUMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_


#include "analysis/spectrum_processing/SpectrumList_SavitzkyGolaySmoother.hpp"
#include "utility/misc/IntegerSet.hpp"

namespace pwiz {
namespace CLI {
namespace msdata {


/// SpectrumList implementation to smooth intensities with SG method
public ref class SpectrumList_SavitzkyGolaySmoother : public SpectrumList
{
    internal: //SpectrumList_NativeCentroider(pwiz::analysis::SpectrumList_NativeCentroider* base)
              //: SpectrumList((boost::shared_ptr<pwiz::msdata::SpectrumList>*) base), base_(base) {}
              virtual ~SpectrumList_SavitzkyGolaySmoother()
              {if (base_) delete base_; if (SpectrumList::base_) delete SpectrumList::base_;}
              pwiz::analysis::SpectrumList_SavitzkyGolaySmoother* base_;

    public:

    SpectrumList_SavitzkyGolaySmoother(SpectrumList^ inner,
                                       System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth)
    : SpectrumList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToSmooth)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::SpectrumList_SavitzkyGolaySmoother(*inner->base_, msLevelSet);
        SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_SavitzkyGolaySmoother::accept(*inner->base_);}
};


} // namespace analysis
} // namespace CLI
} // namespace pwiz


#endif // _SPECTRUMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_
