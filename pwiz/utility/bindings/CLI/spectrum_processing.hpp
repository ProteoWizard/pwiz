//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUM_PROCESSING_HPP_CLI_
#define _SPECTRUM_PROCESSING_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
//#include "SpectrumListWrapper.hpp"
#include "SharedCLI.hpp"
#include "SpectrumList_PeakFilter.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/logic/tribool.hpp>
#pragma warning( pop )


using boost::shared_ptr;


namespace pwiz {
namespace CLI {


namespace msdata {


/// <summary>
/// Inheritable pass-through implementation for wrapping a SpectrumList 
/// </summary>
public ref class SpectrumListWrapper : public msdata::SpectrumList
{
   internal: virtual ~SpectrumListWrapper()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::msdata::SpectrumListWrapper* base_;

    public:

    SpectrumListWrapper(SpectrumList^ inner)
    : SpectrumList(0)
    {
        base_ = new pwiz::msdata::SpectrumListWrapper(*inner->base_);
        SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }
};


} // namespace msdata


namespace analysis {


/// <summary>
/// Delegate for filtering spectra in SpectrumList_Filter
/// </summary>
public delegate bool SpectrumList_FilterAcceptSpectrum(msdata::Spectrum^);
private delegate bool SpectrumList_FilterAcceptSpectrumWrapper(const pwiz::msdata::Spectrum*);

/// <summary>
/// Base predicate intended to be overriden with custom predicate for filtering spectra in SpectrumList_Filter
/// </summary>
class SpectrumList_FilterPredicate : public pwiz::analysis::SpectrumList_Filter::Predicate
{
    bool(*filterDelegatePtr)(const pwiz::msdata::Spectrum*);

    public:
    SpectrumList_FilterPredicate(void* filterDelegatePtr)
        : filterDelegatePtr((bool(*)(const pwiz::msdata::Spectrum*)) filterDelegatePtr)
    {}

    virtual boost::logic::tribool accept(const pwiz::msdata::SpectrumIdentity& spectrumIdentity) const
    {
        return boost::logic::tribool(boost::logic::indeterminate);
    }

    virtual bool accept(const pwiz::msdata::Spectrum& spectrum) const
    {
        return filterDelegatePtr(&spectrum);
    }

    virtual bool done()
    {
        return false;
    }
};

// null deallactor to create shared_ptrs that do not delete when reset
void nullDeallocate(pwiz::msdata::Spectrum* s)
{
    // do nothing
}

/// <summary>
/// SpectrumList implementation filtered by a user's predicate
/// </summary>
public ref class SpectrumList_Filter : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_Filter()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_Filter* base_;

    /*public interface IPredicate
    {
        /// return values:
        ///  true: accept the Spectrum
        ///  false: reject the Spectrum
        ///  indeterminate: need to see the full Spectrum object to decide
        public pwiz::CLI::util::tribool accept(msdata::SpectrumIdentity^ spectrumIdentity);

        /// return true iff Spectrum is accepted
        public bool accept(msdata::Spectrum^ spectrum);

        /// return true iff done accepting spectra; 
        /// this allows early termination of the iteration through the original
        /// SpectrumList, possibly using assumptions about the order of the
        /// iteration (e.g. index is increasing, nativeID interpreted as scan number is
        /// increasing, ...)
        public bool done();
    }*/

    private:

    SpectrumList_FilterAcceptSpectrum^ managedPredicate;

    bool marshal(const pwiz::msdata::Spectrum* s)
    {
        // assume the managed predicate won't change the spectrum
        // use null deallocator because this spectrum pointer comes from a const reference
        pwiz::msdata::SpectrumPtr* s2 =
            new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(s), nullDeallocate);
        msdata::Spectrum^ s3 = gcnew msdata::Spectrum(s2);
        return managedPredicate(s3);
    }


    public:

    SpectrumList_Filter(msdata::SpectrumList^ inner,
                        SpectrumList_FilterAcceptSpectrum^ predicate)
    : msdata::SpectrumList(0), managedPredicate(predicate)
    {
        SpectrumList_FilterAcceptSpectrumWrapper^ wrapper = gcnew SpectrumList_FilterAcceptSpectrumWrapper(this, &SpectrumList_Filter::marshal);
        System::IntPtr predicatePtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(wrapper);
        base_ = new pwiz::analysis::SpectrumList_Filter(*inner->base_, SpectrumList_FilterPredicate(predicatePtr.ToPointer()));
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }
};


/// <summary>
/// Delegate for comparing spectra in SpectrumList_Sorter
/// </summary>
public delegate bool SpectrumList_Sorter_LessThan(msdata::Spectrum^, msdata::Spectrum^);
private delegate bool SpectrumList_Sorter_LessThanWrapper(const pwiz::msdata::Spectrum*, const pwiz::msdata::Spectrum*);

/// <summary>
/// Base predicate intended to be overriden with custom predicate for comparing spectra in SpectrumList_Sorter
/// </summary>
class SpectrumList_SorterPredicate : public pwiz::analysis::SpectrumList_Sorter::Predicate
{
    bool(*sorterDelegatePtr)(const pwiz::msdata::Spectrum*, const pwiz::msdata::Spectrum*);

    public:
    SpectrumList_SorterPredicate(void* sorterDelegatePtr)
        : sorterDelegatePtr((bool(*)(const pwiz::msdata::Spectrum*,
                                     const pwiz::msdata::Spectrum*)) sorterDelegatePtr)
    {}

    virtual boost::logic::tribool accept(const pwiz::msdata::SpectrumIdentity& lhsIdentity,
                                         const pwiz::msdata::SpectrumIdentity& rhsIdentity) const
    {
        return boost::logic::tribool(boost::logic::indeterminate);
    }

    virtual bool accept(const pwiz::msdata::Spectrum& lhs,
                        const pwiz::msdata::Spectrum& rhs) const
    {
        return sorterDelegatePtr(&lhs, &rhs);
    }
};

/// <summary>
/// SpectrumList implementation sorted by a user's predicate
/// </summary>
public ref class SpectrumList_Sorter : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_Sorter()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_Sorter* base_;

    private:

    SpectrumList_Sorter_LessThan^ managedPredicate;

    bool marshal(const pwiz::msdata::Spectrum* lhs, const pwiz::msdata::Spectrum* rhs)
    {
        // assume the managed predicate won't change the spectrum
        // use null deallocator because this spectrum pointer comes from a const reference
        pwiz::msdata::SpectrumPtr* lhs2 = new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(lhs), nullDeallocate);
        pwiz::msdata::SpectrumPtr* rhs2 = new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(rhs), nullDeallocate);
        msdata::Spectrum^ lhs3 = gcnew msdata::Spectrum(lhs2);
        msdata::Spectrum^ rhs3 = gcnew msdata::Spectrum(rhs2);
        return managedPredicate(lhs3, rhs3);
    }


    public:

    SpectrumList_Sorter(msdata::SpectrumList^ inner,
                        SpectrumList_Sorter_LessThan^ predicate)
    : msdata::SpectrumList(0), managedPredicate(predicate)
    {
        SpectrumList_Sorter_LessThanWrapper^ wrapper = gcnew SpectrumList_Sorter_LessThanWrapper(this, &SpectrumList_Sorter::marshal);
        System::IntPtr predicatePtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(wrapper);
        base_ = new pwiz::analysis::SpectrumList_Sorter(*inner->base_, SpectrumList_SorterPredicate(predicatePtr.ToPointer()));
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }
};


public ref class Smoother abstract
{
    internal:
    pwiz::analysis::SmootherPtr* base_;
};

public ref class SavitzkyGolaySmoother : public Smoother
{
    public:
    SavitzkyGolaySmoother(int polynomialOrder, int windowSize)
    {
        base_ = new pwiz::analysis::SmootherPtr(
            new pwiz::analysis::SavitzkyGolaySmoother(polynomialOrder, windowSize));
    }
};

public ref class WhittakerSmoother : public Smoother
{
    public:
    WhittakerSmoother(double lambdaCoefficient)
    {
        base_ = new pwiz::analysis::SmootherPtr(
            new pwiz::analysis::WhittakerSmoother(lambdaCoefficient));
    }
};

/// <summary>
/// SpectrumList implementation to smooth intensities
/// </summary>
public ref class SpectrumList_Smoother : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_Smoother()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_Smoother* base_;

    public:

    SpectrumList_Smoother(msdata::SpectrumList^ inner,
                          Smoother^ algorithm,
                          System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth)
    : msdata::SpectrumList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToSmooth)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::SpectrumList_Smoother(*inner->base_, *algorithm->base_, msLevelSet);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_Smoother::accept(*inner->base_);}
};


public ref class PeakDetector abstract
{
    internal:
    pwiz::analysis::PeakDetectorPtr* base_;
};

public ref class LocalMaximumPeakDetector : public PeakDetector
{
    public:
    LocalMaximumPeakDetector(unsigned int windowSize)
    {
        base_ = new pwiz::analysis::PeakDetectorPtr(new pwiz::analysis::LocalMaximumPeakDetector(windowSize));
    }
};

/// <summary>
/// SpectrumList implementation to replace peak profiles with picked peaks
/// </summary>
public ref class SpectrumList_PeakPicker : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_PeakPicker()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_PeakPicker* base_;

    public:

    SpectrumList_PeakPicker(msdata::SpectrumList^ inner,
                            PeakDetector^ algorithm,
                            bool preferVendorPeakPicking,
                            System::Collections::Generic::IEnumerable<int>^ msLevelsToPeakPick)
    : msdata::SpectrumList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToPeakPick)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::SpectrumList_PeakPicker(*inner->base_, *algorithm->base_, preferVendorPeakPicking, msLevelSet);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_PeakPicker::accept(*inner->base_);}
};


/// <summary>
/// SpectrumList implementation that assigns (probable) charge states to tandem mass spectra
/// </summary>
public ref class SpectrumList_ChargeStateCalculator : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_ChargeStateCalculator()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_ChargeStateCalculator* base_;

    public:

    SpectrumList_ChargeStateCalculator(msdata::SpectrumList^ inner,
                                       bool overrideExistingChargeState,
                                       int maxMultipleCharge,
                                       int minMultipleCharge,
                                       double intensityFractionBelowPrecursorForSinglyCharged)
    : msdata::SpectrumList(0)
    {
        base_ = new pwiz::analysis::SpectrumList_ChargeStateCalculator(
                    *inner->base_, 
                    overrideExistingChargeState,
                    maxMultipleCharge,
                    minMultipleCharge,
                    intensityFractionBelowPrecursorForSinglyCharged);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    /// charge calculation works on any SpectrumList
    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_ChargeStateCalculator::accept(*inner->base_);}
};


} // namespace analysis
} // namespace CLI
} // namespace pwiz

#endif
