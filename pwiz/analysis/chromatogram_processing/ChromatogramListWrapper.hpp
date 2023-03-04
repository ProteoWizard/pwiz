//
// $Id$
//
//
// Original author: Eric Purser <Eric.Purser .@. Vanderbilt.edu>
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


#ifndef _CHROMATOGRAMLISTWRAPPER_HPP_ 
#define _CHROMATOGRAMLISTWRAPPER_HPP_ 


#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include <stdexcept>


namespace pwiz {
namespace analysis {

using namespace msdata;

/// Inheritable pass-through implementation for wrapping a ChromatogramList 
class PWIZ_API_DECL ChromatogramListWrapper : public ChromatogramListBase
{
    public:

    ChromatogramListWrapper(const msdata::ChromatogramListPtr& inner)
        : inner_(inner)
    {
        if (!inner.get()) throw std::runtime_error("[ChromatogramListWrapper] Null ChromatogramListPtr.");
        setDataProcessingPtr(inner->dataProcessingPtr().get() ? boost::make_shared<DataProcessing>(*inner->dataProcessingPtr())
                                                              : boost::make_shared<DataProcessing>("pwiz_Chromatogram_Processing"));
    }

    static bool accept(const msdata::ChromatogramListPtr& inner) {return true;}

    virtual size_t size() const {return inner_->size();}
    virtual bool empty() const {return inner_->empty();}
    virtual const msdata::ChromatogramIdentity& chromatogramIdentity(size_t index) const {return inner_->chromatogramIdentity(index);} 
    virtual size_t find(const std::string& id) const {return inner_->find(id);}
    virtual msdata::ChromatogramPtr chromatogram(size_t index, bool getBinaryData = false) const { return inner_->chromatogram(index, getBinaryData); }

    protected:

    msdata::ChromatogramListPtr inner_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _CHROMATOGRAMLISTWRAPPER_HPP_ 

