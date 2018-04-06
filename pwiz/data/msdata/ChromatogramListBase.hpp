//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
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


#ifndef _CHROMATOGRAMLISTBASE_HPP_ 
#define _CHROMATOGRAMLISTBASE_HPP_ 


#include "pwiz/data/msdata/MSData.hpp"
#include <stdexcept>


namespace pwiz {
namespace msdata {


/// common functionality for base ChromatogramList implementations
class PWIZ_API_DECL ChromatogramListBase : public ChromatogramList
{
    public:

    /// implementation of ChromatogramList
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const {return dp_;}

    /// set DataProcessing
    virtual void setDataProcessingPtr(DataProcessingPtr dp) {dp_ = dp;}

    const char* polarityStringForFilter(CVID polarityType) const
    {
        return (polarityType == MS_negative_scan) ? "- " : ""; // For backward compatibility, let assumptions about postive ion mode remain
    }

    protected:

    DataProcessingPtr dp_;
};


} // namespace msdata 
} // namespace pwiz


#endif // _CHROMATOGRAMLISTBASE_HPP_ 

