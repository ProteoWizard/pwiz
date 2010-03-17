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


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "spectrum_processing.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/Version.hpp"
#pragma warning( pop )


using boost::shared_ptr;


namespace pwiz {
namespace CLI {


namespace msdata {


SpectrumListWrapper::SpectrumListWrapper(SpectrumList^ inner)
: SpectrumList(0)
{
    base_ = new pwiz::msdata::SpectrumListWrapper(*inner->base_);
    SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}


} // namespace msdata


namespace analysis {


/// <summary>
/// version information for the analysis namespace
/// </summary>
public ref class Version
{
    public:
    static int Major() {return pwiz::analysis::Version::Major();}
    static int Minor() {return pwiz::analysis::Version::Minor();}
    static int Revision() {return pwiz::analysis::Version::Revision();}
    static System::String^ LastModified() {return gcnew System::String(pwiz::analysis::Version::LastModified().c_str());}
    static System::String^ ToString() {return gcnew System::String(pwiz::analysis::Version::str().c_str());}
};


SpectrumList_FilterPredicate::SpectrumList_FilterPredicate(void* filterDelegatePtr)
    : filterDelegatePtr((bool(*)(const pwiz::msdata::Spectrum*)) filterDelegatePtr)
{}

boost::logic::tribool SpectrumList_FilterPredicate::accept(const pwiz::msdata::SpectrumIdentity& spectrumIdentity) const
{
    return boost::logic::tribool(boost::logic::indeterminate);
}

bool SpectrumList_FilterPredicate::accept(const pwiz::msdata::Spectrum& spectrum) const
{
    return filterDelegatePtr(&spectrum);
}

bool SpectrumList_FilterPredicate::done()
{
    return false;
}


// null deallactor to create shared_ptrs that do not delete when reset
void nullDeallocate(pwiz::msdata::Spectrum* s)
{
    // do nothing
}

bool SpectrumList_Filter::marshal(const pwiz::msdata::Spectrum* s)
{
    // assume the managed predicate won't change the spectrum
    // use null deallocator because this spectrum pointer comes from a const reference
    pwiz::msdata::SpectrumPtr* s2 =
        new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(s), nullDeallocate);
    msdata::Spectrum^ s3 = gcnew msdata::Spectrum(s2);
    return managedPredicate(s3);
}

SpectrumList_Filter::SpectrumList_Filter(msdata::SpectrumList^ inner,
                                         SpectrumList_FilterAcceptSpectrum^ predicate)
: msdata::SpectrumList(0), managedPredicate(predicate)
{
    SpectrumList_FilterAcceptSpectrumWrapper^ wrapper = gcnew SpectrumList_FilterAcceptSpectrumWrapper(this, &SpectrumList_Filter::marshal);
    System::IntPtr predicatePtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(wrapper);
    base_ = new pwiz::analysis::SpectrumList_Filter(*inner->base_, SpectrumList_FilterPredicate(predicatePtr.ToPointer()));
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}




SpectrumList_SorterPredicate::SpectrumList_SorterPredicate(void* sorterDelegatePtr)
: sorterDelegatePtr((bool(*)(const pwiz::msdata::Spectrum*,
                             const pwiz::msdata::Spectrum*)) sorterDelegatePtr)
{}

boost::logic::tribool SpectrumList_SorterPredicate::accept(const pwiz::msdata::SpectrumIdentity& lhsIdentity,
                                                           const pwiz::msdata::SpectrumIdentity& rhsIdentity) const
{
    return boost::logic::tribool(boost::logic::indeterminate);
}

bool SpectrumList_SorterPredicate::accept(const pwiz::msdata::Spectrum& lhs,
                                          const pwiz::msdata::Spectrum& rhs) const
{
    return sorterDelegatePtr(&lhs, &rhs);
}




bool SpectrumList_Sorter::marshal(const pwiz::msdata::Spectrum* lhs, const pwiz::msdata::Spectrum* rhs)
{
    // assume the managed predicate won't change the spectrum
    // use null deallocator because this spectrum pointer comes from a const reference
    pwiz::msdata::SpectrumPtr* lhs2 = new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(lhs), nullDeallocate);
    pwiz::msdata::SpectrumPtr* rhs2 = new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(rhs), nullDeallocate);
    msdata::Spectrum^ lhs3 = gcnew msdata::Spectrum(lhs2);
    msdata::Spectrum^ rhs3 = gcnew msdata::Spectrum(rhs2);
    return managedPredicate(lhs3, rhs3);
}

SpectrumList_Sorter::SpectrumList_Sorter(msdata::SpectrumList^ inner,
                                         SpectrumList_Sorter_LessThan^ predicate)
: msdata::SpectrumList(0), managedPredicate(predicate)
{
    SpectrumList_Sorter_LessThanWrapper^ wrapper = gcnew SpectrumList_Sorter_LessThanWrapper(this, &SpectrumList_Sorter::marshal);
    System::IntPtr predicatePtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(wrapper);
    base_ = new pwiz::analysis::SpectrumList_Sorter(*inner->base_, SpectrumList_SorterPredicate(predicatePtr.ToPointer()));
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}




SpectrumList_Smoother::SpectrumList_Smoother(msdata::SpectrumList^ inner,
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

bool SpectrumList_Smoother::accept(msdata::SpectrumList^ inner)
{
    return pwiz::analysis::SpectrumList_Smoother::accept(*inner->base_);
}




SpectrumList_PeakPicker::SpectrumList_PeakPicker(msdata::SpectrumList^ inner,
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

bool SpectrumList_PeakPicker::accept(msdata::SpectrumList^ inner)
{
    return pwiz::analysis::SpectrumList_PeakPicker::accept(*inner->base_);
}




SpectrumList_ChargeStateCalculator::SpectrumList_ChargeStateCalculator(
                                   msdata::SpectrumList^ inner,
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

bool SpectrumList_ChargeStateCalculator::accept(msdata::SpectrumList^ inner)
{
    return pwiz::analysis::SpectrumList_ChargeStateCalculator::accept(*inner->base_);
}


} // namespace analysis
} // namespace CLI
} // namespace pwiz
