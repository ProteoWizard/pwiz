//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#define PWIZ_SOURCE

#include "SpectrumIterator.hpp"
#include "MSData.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace msdata {


using namespace util;


namespace {
SpectrumListSimple pastEndMarker_;
SpectrumIterator::Sieve defaultSieve_;
} // namespace


class SpectrumIterator::Impl
{
    public:
    
    Impl();
    Impl(const SpectrumList& spectrumList, const Config& config);

    void preincrement();
    const Spectrum& dereference() const;
    const Spectrum* dereferencePointer() const;
    bool equal(const Impl& that) const;
    bool notEqual(const Impl& that) const;

    private:

    const SpectrumList& spectrumList_;
    IntegerSet scanNumbers_;
    const Sieve& sieve_;
    bool getBinaryData_;

    IntegerSet::const_iterator currentScanNumber_;
    size_t currentIndex_;

    mutable SpectrumPtr spectrum_;
    mutable bool spectrumCached_;

    bool done() const;
    void advanceIndex();
    void advanceToValidScanNumber();
    void advanceToAcceptedSpectrum();
    void updateSpectrum() const;
};


SpectrumIterator::Impl::Impl()
:   spectrumList_(pastEndMarker_), sieve_(defaultSieve_), getBinaryData_(false), 
    currentIndex_(0), spectrumCached_(false)
{}


SpectrumIterator::Impl::Impl(const SpectrumList& spectrumList, const Config& config)
:   spectrumList_(spectrumList), 
    scanNumbers_(config.scanNumbers ? *config.scanNumbers : IntegerSet()),
    sieve_(config.sieve ? *config.sieve : defaultSieve_),
    getBinaryData_(config.getBinaryData), 
    currentScanNumber_(scanNumbers_.begin()),
    currentIndex_(0),
    spectrumCached_(false)
{
    advanceToValidScanNumber();
    advanceToAcceptedSpectrum();
}


void SpectrumIterator::Impl::preincrement() 
{   
    advanceIndex();
    advanceToAcceptedSpectrum();
}


const Spectrum& SpectrumIterator::Impl::dereference() const 
{
    updateSpectrum();
    if (!spectrum_.get())
        throw runtime_error("[SpectrumIterator::dereference()] Invalid pointer.");
    return *spectrum_;
}


const Spectrum* SpectrumIterator::Impl::dereferencePointer() const 
{
    updateSpectrum();
    if (!spectrum_.get())
        throw runtime_error("[SpectrumIterator::dereferencePointer()] Invalid pointer.");
    return spectrum_.get();
}


bool SpectrumIterator::Impl::equal(const Impl& that) const 
{
    return (done() && &that.spectrumList_==&pastEndMarker_ ||
            &spectrumList_==&pastEndMarker_ && that.done() ||
            &spectrumList_==&that.spectrumList_ && currentIndex_==that.currentIndex_);
}


bool SpectrumIterator::Impl::notEqual(const Impl& that) const 
{
    return !equal(that);
} 


bool SpectrumIterator::Impl::done() const 
{
    // return true iff we've exhausted either scan numbers or indices 

    return !scanNumbers_.empty() && currentScanNumber_==scanNumbers_.end() ||
           currentIndex_ >= spectrumList_.size();
} 


void SpectrumIterator::Impl::advanceIndex()
{
    // clear cache and take a step forward

    spectrumCached_ = false;
    spectrum_ = SpectrumPtr();

    if (scanNumbers_.empty())
    {
        // no scan numbers specified -- iterate by index
        currentIndex_++;
    }
    else
    {
        // go the next valid scan number in the list
        currentScanNumber_++;
        advanceToValidScanNumber();
    }
}


void SpectrumIterator::Impl::advanceToValidScanNumber()
{
    // ensure that currentIndex_ matches currentScanNumber_ 

    for (; currentScanNumber_!=scanNumbers_.end(); ++currentScanNumber_)
    {
        currentIndex_ = spectrumList_.find(lexical_cast<string>(*currentScanNumber_)); // TODO: fix
        if (currentIndex_ < spectrumList_.size())
            break; 
    }
}


void SpectrumIterator::Impl::advanceToAcceptedSpectrum()
{
    // advance (if necessary) until sieve_ finds acceptable spectrum

    while (!done())
    {
        spectrum_ = spectrumList_.spectrum(currentIndex_, false);
        if (!spectrum_.get())
            throw runtime_error("[SpectrumIterator::advanceToAcceptedSpectrum()] Invalid pointer.");

        if (sieve_.accept(*spectrum_))
        {
            if (!getBinaryData_) spectrumCached_ = true;
            break;
        }

        advanceIndex();
    }
}


void SpectrumIterator::Impl::updateSpectrum() const
{
    // lazy evaluation of our current Spectrum, allowing for  
    // more efficient temporary copies in for/for_each.  

    if (done())
        throw runtime_error("[SpectrumIterator] Invalid dereference.");
    
    if (!spectrumCached_)
    {
        spectrum_ = spectrumList_.spectrum(currentIndex_, getBinaryData_);
        if (!spectrum_.get())
            throw runtime_error("[SpectrumIterator::updateSpectrum()] Invalid pointer.");

        spectrumCached_ = true;
    }
}


//
// SpectrumIterator forwarding functions
//


PWIZ_API_DECL SpectrumIterator::SpectrumIterator() 
:   impl_(new Impl) 
{}


PWIZ_API_DECL SpectrumIterator::SpectrumIterator(const SpectrumList& spectrumList, const Config& config)
:   impl_(new Impl(spectrumList, config)) 
{}


PWIZ_API_DECL SpectrumIterator::SpectrumIterator(const MSData& msd, const Config& config)
{
    if (!msd.run.spectrumListPtr.get())
        throw runtime_error("[SpectrumIterator::SpectrumIterator(MSData&)] Null spectrumListPtr.");

    impl_ = shared_ptr<Impl>(new Impl(*msd.run.spectrumListPtr, config));
}


PWIZ_API_DECL SpectrumIterator::SpectrumIterator(const SpectrumIterator& that) 
:   impl_(new Impl(*that.impl_)) // uses compiler-generated Impl(Impl&)
{}


PWIZ_API_DECL SpectrumIterator& SpectrumIterator::operator++() {impl_->preincrement(); return *this;}
PWIZ_API_DECL const Spectrum& SpectrumIterator::operator*() const {return impl_->dereference();}
PWIZ_API_DECL const Spectrum* SpectrumIterator::operator->() const {return impl_->dereferencePointer();}
PWIZ_API_DECL bool SpectrumIterator::operator==(const SpectrumIterator& that) const {return impl_->equal(*that.impl_);}
PWIZ_API_DECL bool SpectrumIterator::operator!=(const SpectrumIterator& that) const {return impl_->notEqual(*that.impl_);}


} // namespace msdata
} // namespace pwiz


