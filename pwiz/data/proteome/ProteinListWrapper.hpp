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


#ifndef _PROTEINLISTWRAPPER_HPP_
#define _PROTEINLISTWRAPPER_HPP_


#include "pwiz/data/proteome/ProteomeData.hpp"
#include <stdexcept>


namespace pwiz {
namespace proteome {


/// Inheritable pass-through implementation for wrapping a ProteinList 
class ProteinListWrapper : public ProteinList
{
    public:

    ProteinListWrapper(const ProteinListPtr& inner)
    :   inner_(inner)
    {
        if (!inner.get()) throw std::runtime_error("[ProteinListWrapper] Null ProteinListPtr.");
    }

    virtual size_t size() const {return inner_->size();}

    virtual ProteinPtr protein(size_t index, bool getSequence = true) const {return inner_->protein(index, getSequence);}

    protected:

    ProteinListPtr inner_;
};


} // namespace proteome 
} // namespace pwiz


#endif // _PROTEINLISTWRAPPER_HPP_ 
