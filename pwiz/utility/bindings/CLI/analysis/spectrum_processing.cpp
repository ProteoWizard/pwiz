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
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/Version.hpp"
#include "boost/foreach_field.hpp"
#pragma warning( pop )

namespace b = pwiz::analysis;



namespace pwiz {
namespace CLI {


namespace msdata {


// provides a concrete implementation of spectrum(size_t, bool)
class SpectrumListWrapperDefault : public pwiz::msdata::SpectrumListWrapper
{
    public:
    SpectrumListWrapperDefault(const pwiz::msdata::SpectrumListPtr& inner) : pwiz::msdata::SpectrumListWrapper(inner) {}
    virtual pwiz::msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const {return inner_->spectrum(index, getBinaryData);}
};


SpectrumListWrapper::SpectrumListWrapper(SpectrumList^ inner)
: SpectrumList(0)
{
    base_ = new SpectrumListWrapperDefault(*inner->base_);
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
    static int Major() {return b::Version::Major();}
    static int Minor() {return b::Version::Minor();}
    static int Revision() {return b::Version::Revision();}
    static System::String^ LastModified() {return ToSystemString(b::Version::LastModified());}
    static System::String^ ToString() {return ToSystemString(b::Version::str());}
};


void SpectrumListFactory::wrap(msdata::MSData^ msd, System::String^ wrapper)
{
    wrap(msd, wrapper, nullptr);
}

void SpectrumListFactory::wrap(msdata::MSData^ msd, System::String^ wrapper, util::IterationListenerRegistry^ ilr)
{
    b::SpectrumListFactory::wrap(msd->base(), ToStdString(wrapper), ilr ? &ilr->base() : nullptr);
}

void SpectrumListFactory::wrap(msdata::MSData^ msd, System::Collections::Generic::IList<System::String^>^ wrappers)
{
    wrap(msd, wrappers, nullptr);
}

void SpectrumListFactory::wrap(msdata::MSData^ msd, System::Collections::Generic::IList<System::String^>^ wrappers, util::IterationListenerRegistry^ ilr)
{
    std::vector<std::string> nativeWrappers;
    for each(System::String^ wrapper in wrappers)
        nativeWrappers.push_back(ToStdString(wrapper));
    b::SpectrumListFactory::wrap(msd->base(), nativeWrappers, ilr ? &ilr->base() : nullptr);
}

System::String^ SpectrumListFactory::usage()
{
    return ToSystemString(b::SpectrumListFactory::usage());
}


// delegates accept() to managed code
class SpectrumList_FilterPredicate_Custom : public b::SpectrumList_Filter::Predicate
{
    bool*(*filterDelegatePtr)(const pwiz::msdata::Spectrum*);

    public:
    SpectrumList_FilterPredicate_Custom(void* filterDelegatePtr);

    virtual boost::logic::tribool accept(const pwiz::msdata::SpectrumIdentity& spectrumIdentity) const;

    virtual boost::logic::tribool accept(const pwiz::msdata::Spectrum& spectrum) const;

    virtual bool done() const;

    virtual std::string describe() const { return ""; }
};


SpectrumList_FilterPredicate_Custom::SpectrumList_FilterPredicate_Custom(void* filterDelegatePtr)
    : filterDelegatePtr((bool*(*)(const pwiz::msdata::Spectrum*)) filterDelegatePtr)
{}

boost::logic::tribool SpectrumList_FilterPredicate_Custom::accept(const pwiz::msdata::SpectrumIdentity& spectrumIdentity) const
{
    return boost::logic::indeterminate;
}

boost::logic::tribool SpectrumList_FilterPredicate_Custom::accept(const pwiz::msdata::Spectrum& spectrum) const
{
    bool* result = filterDelegatePtr(&spectrum);
    if (!result)
        return boost::logic::indeterminate;
    bool result2 = *result;
    delete result;
    return result2;
}

bool SpectrumList_FilterPredicate_Custom::done() const
{
    return false;
}


SpectrumList_FilterPredicate_IndexSet::SpectrumList_FilterPredicate_IndexSet(System::String^ indexSet)
{
    IntegerSet parsedIndexSet;
    parsedIndexSet.parse(ToStdString(indexSet));
    base_ = new b::SpectrumList_FilterPredicate_IndexSet(parsedIndexSet);
}

SpectrumList_FilterPredicate_ScanNumberSet::SpectrumList_FilterPredicate_ScanNumberSet(System::String^ scanNumberSet)
{
    IntegerSet parsedIndexSet;
    parsedIndexSet.parse(ToStdString(scanNumberSet));
    base_ = new b::SpectrumList_FilterPredicate_ScanNumberSet(parsedIndexSet);
}

SpectrumList_FilterPredicate_ScanEventSet::SpectrumList_FilterPredicate_ScanEventSet(System::String^ scanEventSet)
{
    IntegerSet parsedIndexSet;
    parsedIndexSet.parse(ToStdString(scanEventSet));
    base_ = new b::SpectrumList_FilterPredicate_ScanEventSet(parsedIndexSet);
}

SpectrumList_FilterPredicate_ScanTimeRange::SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh)
{
    base_ = new b::SpectrumList_FilterPredicate_ScanTimeRange(scanTimeLow, scanTimeHigh);
}

SpectrumList_FilterPredicate_MSLevelSet::SpectrumList_FilterPredicate_MSLevelSet(System::String^ msLevelSet)
{
    IntegerSet parsedIndexSet;
    parsedIndexSet.parse(ToStdString(msLevelSet));
    base_ = new b::SpectrumList_FilterPredicate_MSLevelSet(parsedIndexSet);
}

SpectrumList_FilterPredicate_ChargeStateSet::SpectrumList_FilterPredicate_ChargeStateSet(System::String^ chargeStateSet)
{
    IntegerSet parsedIndexSet;
    parsedIndexSet.parse(ToStdString(chargeStateSet));
    base_ = new b::SpectrumList_FilterPredicate_ChargeStateSet(parsedIndexSet);
}

SpectrumList_FilterPredicate_PrecursorMzSet::SpectrumList_FilterPredicate_PrecursorMzSet(System::Collections::Generic::IEnumerable<double>^ precursorMzSet, chemistry::MZTolerance tolerance, FilterMode mode)
{
    std::set<double> nativeSet;
    for each (double d in precursorMzSet) nativeSet.insert(d);
    base_ = new b::SpectrumList_FilterPredicate_PrecursorMzSet(nativeSet,
                                                               pwiz::chemistry::MZTolerance(tolerance.value, (pwiz::chemistry::MZTolerance::Units) tolerance.units),
                                                               (b::SpectrumList_Filter::Predicate::FilterMode) mode);
}

SpectrumList_FilterPredicate_DefaultArrayLengthSet::SpectrumList_FilterPredicate_DefaultArrayLengthSet(System::String^ defaultArrayLengthSet)
{}

SpectrumList_FilterPredicate_ActivationType::SpectrumList_FilterPredicate_ActivationType(System::Collections::Generic::IEnumerable<CVID>^ filterItem, bool hasNoneOf)
{
    std::set<pwiz::cv::CVID> nativeSet;
    for each (CVID d in filterItem) nativeSet.insert((pwiz::cv::CVID) d);
    base_ = new b::SpectrumList_FilterPredicate_ActivationType(nativeSet, hasNoneOf);
}

SpectrumList_FilterPredicate_AnalyzerType::SpectrumList_FilterPredicate_AnalyzerType(System::Collections::Generic::IEnumerable<CVID>^ filterItem)
{
    std::set<pwiz::cv::CVID> nativeSet;
    for each (CVID d in filterItem) nativeSet.insert((pwiz::cv::CVID) d);
    base_ = new b::SpectrumList_FilterPredicate_AnalyzerType(nativeSet);
}

SpectrumList_FilterPredicate_Polarity::SpectrumList_FilterPredicate_Polarity(CVID polarity)
{
    base_ = new b::SpectrumList_FilterPredicate_Polarity((pwiz::cv::CVID) polarity);
}

//SpectrumList_FilterPredicate_MzPresent::SpectrumList_FilterPredicate_MzPresent(chemistry::MZTolerance mzt, System::Collections::Generic::Generic::ISet<double> mzSet, ThresholdFilter tf, FilterMode mode);

SpectrumList_FilterPredicate_ThermoScanFilter::SpectrumList_FilterPredicate_ThermoScanFilter(System::String^ matchString, bool matchExact, bool inverse)
{
    base_ = new b::SpectrumList_FilterPredicate_ThermoScanFilter(ToStdString(matchString), matchExact, inverse);
}



// null deallactor to create shared_ptrs that do not delete when reset
void nullDeallocate(pwiz::msdata::Spectrum* s)
{
    // do nothing
}

private delegate bool* SpectrumList_FilterAcceptSpectrumWrapper(const pwiz::msdata::Spectrum*);

ref class SpectrumList_Filter::Impl
{
    public:

    SpectrumList_FilterAcceptSpectrum^ userManagedPredicate;
    SpectrumList_FilterPredicate^ managedPredicate;

    Impl(SpectrumList_FilterAcceptSpectrum^ predicate)
    : userManagedPredicate(predicate)
    {
    }

    Impl(SpectrumList_FilterPredicate^ predicate)
    : managedPredicate(predicate)
    {
    }

    bool* marshal(const pwiz::msdata::Spectrum* s)
    {
        // assume the managed predicate won't change the spectrum
        // use null deallocator because this spectrum pointer comes from a const reference
        pwiz::msdata::SpectrumPtr* s2 = new pwiz::msdata::SpectrumPtr(const_cast<pwiz::msdata::Spectrum*>(s), nullDeallocate);
        msdata::Spectrum^ s3 = gcnew msdata::Spectrum(s2);
        System::Nullable<bool> result = userManagedPredicate(s3);
        if (!result.HasValue)
            return 0;
        return new bool(result.Value);
    }
};

SpectrumList_Filter::SpectrumList_Filter(msdata::SpectrumList^ inner,
                                         SpectrumList_FilterAcceptSpectrum^ predicate)
: msdata::SpectrumList(0), impl_(gcnew Impl(predicate))
{
    SpectrumList_FilterAcceptSpectrumWrapper^ wrapper = gcnew SpectrumList_FilterAcceptSpectrumWrapper(impl_, &SpectrumList_Filter::Impl::marshal);
    System::IntPtr predicatePtr = System::Runtime::InteropServices::Marshal::GetFunctionPointerForDelegate(wrapper);
    base_ = new b::SpectrumList_Filter(*inner->base_, SpectrumList_FilterPredicate_Custom(predicatePtr.ToPointer()));
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

SpectrumList_Filter::SpectrumList_Filter(msdata::SpectrumList^ inner,
                                         SpectrumList_FilterPredicate^ predicate)
: msdata::SpectrumList(0), impl_(gcnew Impl(predicate))
{
    base_ = new b::SpectrumList_Filter(*inner->base_, *impl_->managedPredicate->base_);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}





/*class SpectrumList_SorterPredicate : public b::SpectrumList_Sorter::Predicate
{
    (System::Nullable<bool>)(*sorterDelegatePtr)(const pwiz::msdata::Spectrum*, const pwiz::msdata::Spectrum*);

    public:

    SpectrumList_SorterPredicate(void* sorterDelegatePtr)
    : sorterDelegatePtr((System::Nullable<bool>(*)(const pwiz::msdata::Spectrum*,
                                                   const pwiz::msdata::Spectrum*)) sorterDelegatePtr)
    {}

    boost::logic::tribool SpectrumList_SorterPredicate::less(const pwiz::msdata::SpectrumIdentity& lhsIdentity,
                                                             const pwiz::msdata::SpectrumIdentity& rhsIdentity) const
    {
        return boost::logic::indeterminate;
    }

    boost::logic::tribool SpectrumList_SorterPredicate::less(const pwiz::msdata::Spectrum& lhs,
                                                             const pwiz::msdata::Spectrum& rhs) const
    {
        System::Nullable<bool> result = sorterDelegatePtr(&lhs, &rhs);
        return result.HasValue ? result.Value : boost::logic::indeterminate;
    }
};


private delegate System::Nullable<bool> SpectrumList_Sorter_LessThanWrapper(const pwiz::msdata::Spectrum*, const pwiz::msdata::Spectrum*);

System::Nullable<bool> SpectrumList_Sorter::marshal(const pwiz::msdata::Spectrum* lhs, const pwiz::msdata::Spectrum* rhs)
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
    base_ = new b::SpectrumList_Sorter(*inner->base_, SpectrumList_SorterPredicate(predicatePtr.ToPointer()));
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}*/




SpectrumList_Smoother::SpectrumList_Smoother(msdata::SpectrumList^ inner,
                                             Smoother^ algorithm,
                                             System::Collections::Generic::IEnumerable<int>^ msLevelsToSmooth)
: msdata::SpectrumList(0)
{
    pwiz::util::IntegerSet msLevelSet;
    for each(int i in msLevelsToSmooth)
        msLevelSet.insert(i);
    base_ = new b::SpectrumList_Smoother(*inner->base_, *algorithm->base_, msLevelSet);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

bool SpectrumList_Smoother::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_Smoother::accept(*inner->base_);
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
    base_ = new b::SpectrumList_PeakPicker(*inner->base_, *algorithm->base_, preferVendorPeakPicking, msLevelSet);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

bool SpectrumList_PeakPicker::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_PeakPicker::accept(*inner->base_);
}

bool SpectrumList_PeakPicker::supportsVendorPeakPicking(System::String^ rawpath)
{
    return b::SpectrumList_PeakPicker::supportsVendorPeakPicking(ToStdString(rawpath));
}




SpectrumList_LockmassRefiner::SpectrumList_LockmassRefiner(msdata::SpectrumList^ inner, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance)
    : msdata::SpectrumList(0)
{
        base_ = new b::SpectrumList_LockmassRefiner(*inner->base_, lockmassMzPosScans, lockmassMzNegScans, lockmassTolerance);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

bool SpectrumList_LockmassRefiner::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_LockmassRefiner::accept(*inner->base_);
}




SpectrumList_ChargeStateCalculator::SpectrumList_ChargeStateCalculator(
                                   msdata::SpectrumList^ inner,
                                   bool overrideExistingChargeState,
                                   int maxMultipleCharge,
                                   int minMultipleCharge,
                                   double intensityFractionBelowPrecursorForSinglyCharged)
: msdata::SpectrumList(0)
{
    base_ = new b::SpectrumList_ChargeStateCalculator(
                *inner->base_, 
                overrideExistingChargeState,
                maxMultipleCharge,
                minMultipleCharge,
                intensityFractionBelowPrecursorForSinglyCharged);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

bool SpectrumList_ChargeStateCalculator::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_ChargeStateCalculator::accept(*inner->base_);
}




ContinuousInterval::ContinuousInterval(double begin, double end)
{
    Begin = begin;
    End = end;
}




SpectrumList_3D::SpectrumList_3D(msdata::SpectrumList^ inner)
    : msdata::SpectrumList(0)
{
    base_ = new b::SpectrumList_3D(*inner->base_);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

Spectrum3D^ SpectrumList_3D::spectrum3d(double scanStartTime, System::Collections::Generic::IEnumerable<ContinuousInterval>^ ionMobilityRanges)
{
    boost::icl::interval_set<double> ionMobilityRangesSet;
    for each (ContinuousInterval interval in ionMobilityRanges)
        ionMobilityRangesSet.add(boost::icl::continuous_interval<double>(interval.Begin, interval.End));

    b::Spectrum3DPtr nativeResult = base_->spectrum3d(scanStartTime, ionMobilityRangesSet);

    int i=0;
    Spectrum3D^ result = gcnew Spectrum3D(nativeResult->size());
    for (b::Spectrum3D::const_iterator itr = nativeResult->begin(), end = nativeResult->end(); itr != end; ++itr)
    {
        result->Add(System::Collections::Generic::KeyValuePair<double, Spectrum3DValue^>(itr->first, gcnew Spectrum3DValue(itr->second.size())));
        Spectrum3DValue^ spectrum = result[i++].Value;
        for (b::Spectrum3D::value_type::second_type::const_iterator itr2 = itr->second.begin(), end2 = itr->second.end(); itr2 != end2; ++itr2)
            spectrum->Add(MzIntensityPair(itr2->first, itr2->second));
    }
    return result;
}

bool SpectrumList_3D::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_3D::accept(*inner->base_);
}


SpectrumList_IonMobility::SpectrumList_IonMobility(msdata::SpectrumList^ inner)
    : msdata::SpectrumList(0)
{
    base_ = new b::SpectrumList_IonMobility(*inner->base_);
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

SpectrumList_IonMobility::IonMobilityUnits SpectrumList_IonMobility::getIonMobilityUnits()
{
    try { return static_cast<SpectrumList_IonMobility::IonMobilityUnits>(base_->getIonMobilityUnits()); } CATCH_AND_FORWARD
}

bool SpectrumList_IonMobility::hasCombinedIonMobility()
{
    try { return base_->hasCombinedIonMobility(); } CATCH_AND_FORWARD
}

bool SpectrumList_IonMobility::canConvertIonMobilityAndCCS(IonMobilityUnits units)
{
    try { return base_->canConvertIonMobilityAndCCS(static_cast<b::SpectrumList_IonMobility::IonMobilityUnits>(units)); } CATCH_AND_FORWARD
}

double SpectrumList_IonMobility::ionMobilityToCCS(double ionMobility, double mz, int charge)
{
    try { return base_->ionMobilityToCCS(ionMobility, mz, charge); } CATCH_AND_FORWARD
}

double SpectrumList_IonMobility::ccsToIonMobility(double ccs, double mz, int charge)
{
    try { return base_->ccsToIonMobility(ccs, mz, charge); } CATCH_AND_FORWARD
}

bool SpectrumList_IonMobility::accept(msdata::SpectrumList^ inner)
{
    return b::SpectrumList_IonMobility::accept(*inner->base_);
}


SpectrumList_DiaUmpire::Config::TargetWindow::TargetWindow(double start, double end)
{
    base_ = new DiaUmpire::TargetWindow(DiaUmpire::MzRange(start, end));
}

SpectrumList_DiaUmpire::Config::TargetWindowList^ SpectrumList_DiaUmpire::Config::diaVariableWindows::get()
{
    return gcnew SpectrumList_DiaUmpire::Config::TargetWindowList(&base().diaVariableWindows, this);
}

SpectrumList_DiaUmpire::Config::Config()
{
    base_ = new DiaUmpire::Config();
}

SpectrumList_DiaUmpire::SpectrumList_DiaUmpire(msdata::MSData^ msd, msdata::SpectrumList^ inner, Config^ config)
    : msdata::SpectrumList(0)
{
    try { base_ = new b::SpectrumList_DiaUmpire(msd->base(), *inner->base_, config->base()); } CATCH_AND_FORWARD
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}

SpectrumList_DiaUmpire::SpectrumList_DiaUmpire(msdata::MSData^ msd, msdata::SpectrumList^ inner, Config^ config, util::IterationListenerRegistry^ ilr)
    : msdata::SpectrumList(0)
{
    try { base_ = new b::SpectrumList_DiaUmpire(msd->base(), *inner->base_, config->base(), &ilr->base()); } CATCH_AND_FORWARD
    msdata::SpectrumList::base_ = new boost::shared_ptr<pwiz::msdata::SpectrumList>(base_);
}


ChromatogramList_XICGenerator::ChromatogramList_XICGenerator(msdata::ChromatogramList^ inner)
    : msdata::ChromatogramList(0)
{
    base_ = new b::ChromatogramList_XICGenerator(*inner->base_);
    msdata::ChromatogramList::base_ = new boost::shared_ptr<pwiz::msdata::ChromatogramList>(base_);
}

msdata::Chromatogram^ ChromatogramList_XICGenerator::xic(double startTime, double endTime, System::Collections::Generic::IEnumerable<ContinuousInterval>^ massRanges, int msLevel)
{
    boost::icl::interval_set<double> massRangesSet;
    for each (ContinuousInterval interval in massRanges)
        massRangesSet.add(boost::icl::continuous_interval<double>(interval.Begin, interval.End));

    try { return gcnew msdata::Chromatogram(new pwiz::msdata::ChromatogramPtr(base_->xic(startTime, endTime, massRangesSet, msLevel))); } CATCH_AND_FORWARD
}

bool ChromatogramList_XICGenerator::accept(msdata::ChromatogramList^ inner)
{
    return b::ChromatogramList_XICGenerator::accept(*inner->base_);
}


} // namespace analysis
} // namespace CLI
} // namespace pwiz
