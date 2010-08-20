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

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../msdata/MSData.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend
    #using "pwiz_bindings_cli_msdata.dll" as_friend
#endif

#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Smoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/logic/tribool.hpp>
#pragma warning( pop )


#define DEFINE_INTERNAL_LIST_WRAPPER_CODE(CLIType, NativeType) \
INTERNAL: virtual ~CLIType() {/* base class destructor will delete the shared pointer */} \
          NativeType* base_; \
          NativeType& base() {return *base_;}


using boost::shared_ptr;


namespace pwiz {
namespace CLI {


namespace msdata {


/// <summary>
/// Inheritable pass-through implementation for wrapping a SpectrumList 
/// </summary>
public ref class SpectrumListWrapper : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumListWrapper, pwiz::msdata::SpectrumListWrapper)

    public:

    SpectrumListWrapper(SpectrumList^ inner);
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
    SpectrumList_FilterPredicate(void* filterDelegatePtr);

    virtual boost::logic::tribool accept(const pwiz::msdata::SpectrumIdentity& spectrumIdentity) const;

    virtual bool accept(const pwiz::msdata::Spectrum& spectrum) const;

    virtual bool done();
};

/* TODO: bind to these built-in predicates

public ref class SpectrumList_FilterPredicate_IndexSet : public SpectrumList_FilterPredicate
{
    public:
    SpectrumList_FilterPredicate_IndexSet(const util::IntegerSet& indexSet);
};


public ref class SpectrumList_FilterPredicate_ScanNumberSet : public SpectrumList_FilterPredicate
{
    public:
    SpectrumList_FilterPredicate_ScanNumberSet(const util::IntegerSet& scanNumberSet);
};


public ref class SpectrumList_FilterPredicate_ScanEventSet : public SpectrumList_FilterPredicate
{
    public:
    SpectrumList_FilterPredicate_ScanEventSet(const util::IntegerSet& scanEventSet);
};


public ref class SpectrumList_FilterPredicate_ScanTimeRange : public SpectrumList_FilterPredicate
{
    public:
    SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh);
};


public ref class SpectrumList_FilterPredicate_MSLevelSet : public SpectrumList_FilterPredicate
{
    public:
    SpectrumList_FilterPredicate_MSLevelSet(const util::IntegerSet& msLevelSet);
}
*/


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


/// <summary>
/// SpectrumList implementation filtered by a user's predicate
/// </summary>
public ref class SpectrumList_Filter : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Filter, pwiz::analysis::SpectrumList_Filter)

    public:

    SpectrumList_Filter(msdata::SpectrumList^ inner,
                        SpectrumList_FilterAcceptSpectrum^ predicate);


    private:

    SpectrumList_FilterAcceptSpectrum^ managedPredicate;
    bool marshal(const pwiz::msdata::Spectrum* s);
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
    SpectrumList_SorterPredicate(void* sorterDelegatePtr);

    virtual boost::logic::tribool accept(const pwiz::msdata::SpectrumIdentity& lhsIdentity,
                                         const pwiz::msdata::SpectrumIdentity& rhsIdentity) const;

    virtual bool accept(const pwiz::msdata::Spectrum& lhs,
                        const pwiz::msdata::Spectrum& rhs) const;
};

/// <summary>
/// SpectrumList implementation sorted by a user's predicate
/// </summary>
public ref class SpectrumList_Sorter : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Sorter, pwiz::analysis::SpectrumList_Sorter)

    public:

    SpectrumList_Sorter(msdata::SpectrumList^ inner,
                        SpectrumList_Sorter_LessThan^ predicate);
    

    private:

    SpectrumList_Sorter_LessThan^ managedPredicate;
    bool marshal(const pwiz::msdata::Spectrum* lhs, const pwiz::msdata::Spectrum* rhs);
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
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_Smoother, pwiz::analysis::SpectrumList_Smoother)

    public:

    SpectrumList_Smoother(msdata::SpectrumList^ inner,
                          Smoother^ algorithm,
                          System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth);

    static bool accept(msdata::SpectrumList^ inner);
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
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_PeakPicker, pwiz::analysis::SpectrumList_PeakPicker)

    public:

    SpectrumList_PeakPicker(msdata::SpectrumList^ inner,
                            PeakDetector^ algorithm,
                            bool preferVendorPeakPicking,
                            System::Collections::Generic::IEnumerable<int>^ msLevelsToPeakPick);

    static bool accept(msdata::SpectrumList^ inner);
};


/// <summary>
/// SpectrumList implementation that assigns (probable) charge states to tandem mass spectra
/// </summary>
public ref class SpectrumList_ChargeStateCalculator : public msdata::SpectrumList
{
    DEFINE_INTERNAL_LIST_WRAPPER_CODE(SpectrumList_ChargeStateCalculator, pwiz::analysis::SpectrumList_ChargeStateCalculator)

    public:

    SpectrumList_ChargeStateCalculator(msdata::SpectrumList^ inner,
                                       bool overrideExistingChargeState,
                                       int maxMultipleCharge,
                                       int minMultipleCharge,
                                       double intensityFractionBelowPrecursorForSinglyCharged);

    /// charge calculation works on any SpectrumList
    static bool accept(msdata::SpectrumList^ inner);
};


} // namespace analysis
} // namespace CLI
} // namespace pwiz

#endif
