//
// $Id$
//
//
// Original author: Eric Purser <Eric.Purser@Vanderbilt.edu>
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


#ifndef _CHROMATOGRAMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_
#define _CHROMATOGRAMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_


#include "pwiz/analysis/chromatogram_processing/ChromatogramList_SavitzkyGolaySmoother.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"

namespace pwiz {
namespace CLI {
namespace msdata {


/// ChromatogramList implementation to smooth intensities with SG method
public ref class ChromatogramList_SavitzkyGolaySmoother : public ChromatogramList
{
    internal: //ChromatogramList_NativeCentroider(pwiz::analysis::ChromatogramList_NativeCentroider* base)
              //: ChromatogramList((boost::shared_ptr<pwiz::msdata::ChromatogramList>*) base), base_(base) {}
              virtual ~ChromatogramList_SavitzkyGolaySmoother()
              {if (base_) delete base_; if (ChromatogramList::base_) delete ChromatogramList::base_;}
              pwiz::analysis::ChromatogramList_SavitzkyGolaySmoother* base_;

    public:

    ChromatogramList_SavitzkyGolaySmoother(ChromatogramList^ inner,
                                       System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth)
    : ChromatogramList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToSmooth)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::ChromatogramList_SavitzkyGolaySmoother(*inner->base_, msLevelSet);
        ChromatogramList::base_ = new boost::shared_ptr<pwiz::msdata::ChromatogramList>(base_);
    }

    static bool accept(msdata::ChromatogramList^ inner)
    {return pwiz::analysis::ChromatogramList_SavitzkyGolaySmoother::accept(*inner->base_);}
};


} // namespace analysis
} // namespace CLI
} // namespace pwiz


#endif // _CHROMATOGRAMLIST_SAVITZKYGOLAYSMOOTHER_HPP_CLI_
