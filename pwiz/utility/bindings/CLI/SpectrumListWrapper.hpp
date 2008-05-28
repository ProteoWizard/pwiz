//
// SpectrumListWrapper.hpp
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


#ifndef _SPECTRUMLISTWRAPPER_HPP_CLI_
#define _SPECTRUMLISTWRAPPER_HPP_CLI_


#include "MSData.hpp"
#include "analysis/spectrum_processing/SpectrumListWrapper.hpp"


namespace pwiz {
namespace CLI {
namespace analysis {


/// Inheritable pass-through implementation for wrapping a SpectrumList 
public ref class SpectrumListWrapper : public msdata::SpectrumList
{
    internal: SpectrumListWrapper(boost::shared_ptr<pwiz::analysis::SpectrumListWrapper>* base)
              : msdata::SpectrumList((boost::shared_ptr<pwiz::msdata::SpectrumList>*) base), base_(base) {}
              virtual ~SpectrumListWrapper() {if (base_) delete base_;}
              boost::shared_ptr<pwiz::analysis::SpectrumListWrapper>* base_;

    public:

    SpectrumListWrapper(msdata::SpectrumList^ inner)
    : msdata::SpectrumList(inner->base_),
      inner_(inner)
    {
        if (inner == nullptr) throw gcnew System::Exception("[SpectrumListWrapper] Null inner SpectrumList.");
    }

    virtual int size() new {return inner_->size();}
    virtual bool empty() new {return inner_->empty();}
    virtual msdata::SpectrumIdentity^ spectrumIdentity(int index) new {return inner_->spectrumIdentity(index);} 
    virtual int find(System::String^ id) new {return inner_->find(id);}
    virtual int findNative(System::String^ nativeID) new {return inner_->findNative(nativeID);}
    virtual msdata::Spectrum^ spectrum(int index, bool getBinaryData) new {return inner_->spectrum(index, getBinaryData);}

    protected:

    msdata::SpectrumList^ inner_;
};


} // namespace analysis
} // namespace CLI
} // namespace pwiz


#endif // _SPECTRUMLISTWRAPPER_HPP_CLI_
