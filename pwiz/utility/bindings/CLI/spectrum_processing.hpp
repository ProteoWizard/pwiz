#ifndef _SPECTRUM_PROCESSING_HPP_CLI_
#define _SPECTRUM_PROCESSING_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4635 )
//#include "SpectrumListWrapper.hpp"
#include "SharedCLI.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_NativeCentroider.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_SavitzkyGolaySmoother.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Thresholder.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_ChargeStateCalculator.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/logic/tribool.hpp>
#pragma warning( pop )


using boost::shared_ptr;


namespace pwiz {
namespace CLI {
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


/// <summary>
/// SpectrumList implementation to return native centroided spectrum data
/// </summary>
public ref class SpectrumList_NativeCentroider : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_NativeCentroider()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_NativeCentroider* base_;

    public:

    SpectrumList_NativeCentroider(msdata::SpectrumList^ inner,
                                  System::Collections::Generic::IEnumerable<int>^ msLevelsToCentroid)
    : msdata::SpectrumList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToCentroid)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::SpectrumList_NativeCentroider(*inner->base_, msLevelSet);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_NativeCentroider::accept(*inner->base_);}
};


/// <summary>
/// SpectrumList implementation to smooth intensities with SG method
/// </summary>
public ref class SpectrumList_SavitzkyGolaySmoother : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_SavitzkyGolaySmoother()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_SavitzkyGolaySmoother* base_;

    public:

    SpectrumList_SavitzkyGolaySmoother(msdata::SpectrumList^ inner,
                                       System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth)
    : msdata::SpectrumList(0)
    {
        pwiz::util::IntegerSet msLevelSet;
        for each(int i in msLevelsToSmooth)
            msLevelSet.insert(i);
        base_ = new pwiz::analysis::SpectrumList_SavitzkyGolaySmoother(*inner->base_, msLevelSet);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_SavitzkyGolaySmoother::accept(*inner->base_);}
};


/// <summary>
/// determines the method of thresholding and the meaning of the threshold value
/// </summary>
public enum class ThresholdingBy_Type
{
    /// <summary>
    /// keep the {threshold} [most|least] intense data points
    /// - {threshold} is rounded to the nearest integer
    /// - if the {threshold} falls within equally intense data points, all data points with that intensity are removed
    /// </summary>
    ThresholdingBy_Count,

    /// <summary>
    /// keep the {threshold} [most|least] intense data points
    /// - {threshold} is rounded to the nearest integer
    /// - if the {threshold} falls within equally intense data points, all data points with that intensity are kept
    /// </summary>
    ThresholdingBy_CountAfterTies,

    /// keep data points ranked [better|worse] than {threshold}
    /// - {threshold} is rounded to the nearest integer
    /// - rank 1 is the most intense
    // TODO: By_CompetitionRank,

    /// keep data points ranked [better|worse] than {threshold}
    /// - rank 1 is the most intense
    // TODO: By_FractionalRank,

    /// <summary>
    /// keep data points [more|less] absolutely intense than {threshold}
    /// </summary>
    ThresholdingBy_AbsoluteIntensity,

    /// <summary>
    /// keep data points [more|less] relatively intense than {threshold}
    /// - {threshold} is each data point's fraction of the base peak intensity (in the range [0,1])
    /// </summary>
    ThresholdingBy_FractionOfBasePeakIntensity,

    /// <summary>
    /// keep data points [more|less] relatively intense than {threshold}
    /// - {threshold} is each data point's fraction of the total intensity, aka total ion current (in the range [0,1])
    /// </summary>
    ThresholdingBy_FractionOfTotalIntensity,

    /// <summary>
    /// keep data points that are part of the {threshold} [most|least] intense fraction
    /// - {threshold} is the fraction of TIC to keep, i.e. the TIC of the kept data points is {threshold} * original TIC
    /// </summary>
    ThresholdingBy_FractionOfTotalIntensityCutoff
};


/// <summary>
/// determines the orientation of the thresholding
/// </summary>
public enum class ThresholdingOrientation
{
    Orientation_MostIntense, /// <summary>thresholder removes the least intense data points</summary>
    Orientation_LeastIntense /// <summary>thresholder removes the most intense data points</summary>
};


/// <summary>
/// SpectrumList implementation that returns spectra with low or high intensity data points removed (depending on the configuration)
/// </summary>
public ref class SpectrumList_Thresholder : public msdata::SpectrumList
{
    internal: virtual ~SpectrumList_Thresholder()
              {
                  // base class destructor will delete the shared pointer
              }
              pwiz::analysis::SpectrumList_Thresholder* base_;

    public:

    SpectrumList_Thresholder(msdata::SpectrumList^ inner,
                             ThresholdingBy_Type byType,
                             double threshold)
    : msdata::SpectrumList(0)
    {
        base_ = new pwiz::analysis::SpectrumList_Thresholder(
                    *inner->base_, 
                    (pwiz::analysis::ThresholdingBy_Type) byType,
                    threshold);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    SpectrumList_Thresholder(msdata::SpectrumList^ inner,
                             ThresholdingBy_Type byType,
                             double threshold,
                             ThresholdingOrientation orientation)
    : msdata::SpectrumList(0)
    {
        base_ = new pwiz::analysis::SpectrumList_Thresholder(
                    *inner->base_,
                    (pwiz::analysis::ThresholdingBy_Type) byType,
                    threshold,
                    (pwiz::analysis::ThresholdingOrientation) orientation);
        msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
    }

    /// thresholding works on any SpectrumList
    static bool accept(msdata::SpectrumList^ inner)
    {return pwiz::analysis::SpectrumList_Thresholder::accept(*inner->base_);}
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
