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


#ifndef _SPECTRUMLISTWRAPPER_HPP_ 
#define _SPECTRUMLISTWRAPPER_HPP_ 


#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include <boost/pointer_cast.hpp>
#include <stdexcept>


namespace pwiz {
namespace msdata {


/// Inheritable pass-through implementation for wrapping a SpectrumList 
class PWIZ_API_DECL SpectrumListWrapper : public SpectrumListBase
{
    public:

    SpectrumListWrapper(const SpectrumListPtr& inner)
    :   inner_(inner)
    {
        if (!inner.get()) throw std::runtime_error("[SpectrumListWrapper] Null SpectrumListPtr.");
        dp_.reset(inner->dataProcessingPtr().get() ? new DataProcessing(*inner->dataProcessingPtr())
                                                   : new DataProcessing("pwiz_Spectrum_Processing"));
    }

    virtual size_t size() const {return inner_->size();}
    virtual bool empty() const {return size() == 0;}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const {return inner_->spectrumIdentity(index);}
    virtual size_t find(const std::string& id) const {return size() == inner_->size() ? inner_->find(id) : SpectrumList::find(id);}
    virtual size_t findAbbreviated(const std::string& abbreviatedId, char delimiter = '.') const {return size() == inner_->size() ? inner_->findAbbreviated(abbreviatedId, delimiter) : SpectrumList::findAbbreviated(abbreviatedId, delimiter);}
    virtual IndexList findNameValue(const std::string& name, const std::string& value) const {return size() == inner_->size() ? inner_->findNameValue(name, value) : SpectrumList::findNameValue(name, value);}
    virtual IndexList findSpotID(const std::string& spotID) const {return size() == inner_->size() ? inner_->findSpotID(spotID) : SpectrumList::findSpotID(spotID);}

    // no default implementation, because otherwise subclasses could override the DetailLevel overload and the getBinaryData overload would be inconsistent
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const = 0;

    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const {return spectrum(index, detailLevel == DetailLevel_FullData);}

    SpectrumListPtr inner() const {return inner_;}

    SpectrumListPtr innermost() const
    {
        if(dynamic_cast<SpectrumListWrapper*>(&*inner_))
            return dynamic_cast<SpectrumListWrapper*>(&*inner_)->innermost();
        else
            return inner();
    }

    /// return true if either the wrapper implementation would benefit from using multiple threads, or if the inner wrapped list would
    virtual bool benefitsFromWorkerThreads() const
    {
        return innerBenefitsFromWorkerThreads();
    }

    protected:

    SpectrumListPtr inner_;

    bool innerBenefitsFromWorkerThreads() const
    {
        auto innerAsWrapper = boost::dynamic_pointer_cast<SpectrumListWrapper>(inner_);
        return innerAsWrapper != nullptr ? innerAsWrapper->benefitsFromWorkerThreads() : false;
    }
};

typedef boost::shared_ptr<SpectrumListWrapper> SpectrumListWrapperPtr;


} // namespace msdata 
} // namespace pwiz


#endif // _SPECTRUMLISTWRAPPER_HPP_ 

