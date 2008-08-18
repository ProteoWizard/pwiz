#ifndef _SPECTRUM_PROCESSING_HPP_CLI_
#define _SPECTRUM_PROCESSING_HPP_CLI_


//#include "SpectrumListWrapper.hpp"
#include "SharedCLI.hpp"
#include "analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "analysis/spectrum_processing/SpectrumList_Sorter.hpp"
#include "analysis/spectrum_processing/SpectrumList_NativeCentroider.hpp"
#include "analysis/spectrum_processing/SpectrumList_SavitzkyGolaySmoother.hpp"
#include "utility/misc/IntegerSet.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/logic/tribool.hpp>

using boost::shared_ptr;

namespace pwiz {
namespace CLI {
namespace analysis {

public delegate bool SpectrumList_FilterAcceptSpectrum(msdata::Spectrum^);
private delegate bool SpectrumList_FilterAcceptSpectrumWrapper(const pwiz::msdata::Spectrum*);

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

/// SpectrumList implementation filtered by a user's predicate
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


public delegate bool SpectrumList_Sorter_LessThan(msdata::Spectrum^, msdata::Spectrum^);
private delegate bool SpectrumList_Sorter_LessThanWrapper(const pwiz::msdata::Spectrum*, const pwiz::msdata::Spectrum*);

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

/// SpectrumList implementation sorted by a user's predicate
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


/// SpectrumList implementation to return native centroided spectrum data
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


/// SpectrumList implementation to smooth intensities with SG method
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


} // namespace analysis
} // namespace CLI
} // namespace pwiz

#endif
