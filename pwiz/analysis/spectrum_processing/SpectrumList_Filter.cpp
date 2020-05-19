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


#define PWIZ_SOURCE

#include "pwiz/data/common/cv.hpp"
#include "SpectrumList_Filter.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::msdata;

using boost::logic::tribool;


//
// SpectrumList_Filter::Impl
//


struct SpectrumList_Filter::Impl
{
    const SpectrumListPtr original;
    std::vector<SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index
    DetailLevel detailLevel; // the detail level needed for a non-indeterminate result

    Impl(SpectrumListPtr original, const Predicate& predicate, IterationListenerRegistry* ilr);
    void pushSpectrum(const SpectrumIdentity& spectrumIdentity);
};


SpectrumList_Filter::Impl::Impl(SpectrumListPtr _original, const Predicate& predicate, IterationListenerRegistry* ilr)
:   original(_original), detailLevel(predicate.suggestedDetailLevel())
{
    if (!original.get()) throw runtime_error("[SpectrumList_Filter] Null pointer");

    // iterate through the spectra, using predicate to build the sub-list
    for (size_t i=0, end=original->size(); i<end; i++)
    {
        if (ilr)
        {
            if (IterationListener::Status_Cancel == ilr->broadcastUpdateMessage(IterationListener::UpdateMessage(i, original->size(), "filtering spectra (by " + predicate.describe() + ")")))
                break;
        }

        if (predicate.done()) break;

        // first try to determine acceptance based on SpectrumIdentity alone
        const SpectrumIdentity& spectrumIdentity = original->spectrumIdentity(i);
        tribool accepted = predicate.accept(spectrumIdentity);

        if (accepted)
        {
            pushSpectrum(spectrumIdentity);            
        }
        else if (!accepted)
        {
            // do nothing 
        }
        else // indeterminate
        {
            // not enough info -- we need to retrieve the Spectrum
            do
            {
                SpectrumPtr spectrum = original->spectrum(i, detailLevel);
                accepted = predicate.accept(*spectrum);

                if (boost::logic::indeterminate(accepted) && (int) detailLevel < (int) DetailLevel_FullMetadata)
                    detailLevel = DetailLevel(int(detailLevel) + 1);
                else
                {
                    if (accepted)
                       pushSpectrum(spectrumIdentity);
                    break;
                }
            }
            while ((int) detailLevel <= (int) DetailLevel_FullMetadata);
        }
    }
}


void SpectrumList_Filter::Impl::pushSpectrum(const SpectrumIdentity& spectrumIdentity)
{
    indexMap.push_back(spectrumIdentity.index);
    spectrumIdentities.push_back(spectrumIdentity);
    spectrumIdentities.back().index = spectrumIdentities.size()-1;
}


//
// SpectrumList_Filter
//


PWIZ_API_DECL SpectrumList_Filter::SpectrumList_Filter(const SpectrumListPtr original, const Predicate& predicate, IterationListenerRegistry* ilr)
:   SpectrumListWrapper(original), impl_(new Impl(original, predicate, ilr))
{}


PWIZ_API_DECL size_t SpectrumList_Filter::size() const
{
    return impl_->indexMap.size();
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Filter::spectrumIdentity(size_t index) const
{
    return impl_->spectrumIdentities.at(index);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Filter::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Filter::spectrum(size_t index, DetailLevel detailLevel) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    SpectrumPtr originalSpectrum = impl_->original->spectrum(originalIndex, detailLevel);  

    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index;

    return newSpectrum;
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const SpectrumList_Filter::Predicate::FilterMode& mode)
{
    if (mode == SpectrumList_Filter::Predicate::FilterMode_Include)
        os << "include";
    else
        os << "exclude";
    return os;
}


PWIZ_API_DECL std::istream& operator>>(std::istream& is, SpectrumList_Filter::Predicate::FilterMode& mode)
{
    string modeStr;
    is >> modeStr;
    if (bal::iequals(modeStr, "include"))
        mode = SpectrumList_Filter::Predicate::FilterMode_Include;
    else if (bal::iequals(modeStr, "exclude"))
        mode = SpectrumList_Filter::Predicate::FilterMode_Exclude;
    else
        is.setstate(std::ios_base::failbit);
    return is;
}


//
// SpectrumList_FilterPredicate_IndexSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet::SpectrumList_FilterPredicate_IndexSet(const IntegerSet& indexSet)
:   indexSet_(indexSet), eos_(false)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_IndexSet::accept(const SpectrumIdentity& spectrumIdentity) const
{
    if (indexSet_.hasUpperBound((int)spectrumIdentity.index)) eos_ = true;
    bool result = indexSet_.contains((int)spectrumIdentity.index);
    return result;
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_IndexSet::done() const
{
    return eos_; // end of set
}


//
// SpectrumList_FilterPredicate_ScanNumberSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ScanNumberSet::SpectrumList_FilterPredicate_ScanNumberSet(const IntegerSet& scanNumberSet)
:   scanNumberSet_(scanNumberSet), eos_(false)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_ScanNumberSet::accept(const SpectrumIdentity& spectrumIdentity) const
{
    int scanNumber = id::valueAs<int>(spectrumIdentity.id, "scan");
    if (scanNumberSet_.hasUpperBound(scanNumber)) eos_ = true;
    bool result = scanNumberSet_.contains(scanNumber);
    return result;
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_ScanNumberSet::done() const
{
    return eos_; // end of set
}


PWIZ_API_DECL SpectrumList_FilterPredicate_IdSet::SpectrumList_FilterPredicate_IdSet(const set<string>& idSet)
    : idSet_(idSet)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_IdSet::accept(const SpectrumIdentity& spectrumIdentity) const
{
    return idSet_.count(spectrumIdentity.id) > 0;
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_IdSet::done() const
{
    return false;
}


//
// SpectrumList_FilterPredicate_ScanEventSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ScanEventSet::SpectrumList_FilterPredicate_ScanEventSet(const IntegerSet& scanEventSet)
:   scanEventSet_(scanEventSet)
{}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_ScanEventSet::accept(const msdata::Spectrum& spectrum) const
{
    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];
    CVParam param = scan.cvParam(MS_preset_scan_configuration);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    int scanEvent = lexical_cast<int>(param.value);
    bool result = scanEventSet_.contains(scanEvent);
    return result;
}


//
// SpectrumList_FilterPredicate_ScanTimeRange 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ScanTimeRange::SpectrumList_FilterPredicate_ScanTimeRange(double scanTimeLow, double scanTimeHigh, bool assumeSorted)
:   scanTimeLow_(scanTimeLow), scanTimeHigh_(scanTimeHigh), eos_(false), assumeSorted_(assumeSorted)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_ScanTimeRange::accept(const SpectrumIdentity& spectrumIdentity) const
{
    // TODO: encode scan time in mzML index (and SpectrumIdentity)
    return boost::logic::indeterminate;
}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_ScanTimeRange::accept(const msdata::Spectrum& spectrum) const
{
    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];
    CVParam param = scan.cvParam(MS_scan_start_time);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    double time = param.timeInSeconds();

    eos_ = assumeSorted_ && time > scanTimeHigh_;
    return (time>=scanTimeLow_ && time<=scanTimeHigh_);
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_ScanTimeRange::done() const
{
    return eos_; // end of set
}


//
// SpectrumList_FilterPredicate_MSLevelSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_MSLevelSet::SpectrumList_FilterPredicate_MSLevelSet(const IntegerSet& msLevelSet)
:   msLevelSet_(msLevelSet)
{}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_MSLevelSet::accept(const msdata::Spectrum& spectrum) const
{
    CVParam param = spectrum.cvParamChild(MS_spectrum_type);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    if (!cvIsA(param.cvid, MS_mass_spectrum))
        return msLevelSet_.contains(0); // non-MS spectra are considered ms level 0
    param = spectrum.cvParam(MS_ms_level);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    int msLevel = param.valueAs<int>();
    bool result = msLevelSet_.contains(msLevel);
    return result;
}


//
// SpectrumList_FilterPredicate_ChargeStateSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ChargeStateSet::SpectrumList_FilterPredicate_ChargeStateSet(const IntegerSet& chargeStateSet)
:   chargeStateSet_(chargeStateSet)
{}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_ChargeStateSet::accept(const msdata::Spectrum& spectrum) const
{
    CVParam param = spectrum.cvParamChild(MS_spectrum_type);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    if (!cvIsA(param.cvid, MS_mass_spectrum))
        return true; // charge state filter doesn't affect non-MS spectra
    param = spectrum.cvParam(MS_ms_level);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    int msLevel = param.valueAs<int>();
    if (msLevel == 1 || // MS1s don't have charge state
        spectrum.precursors.empty()) // can't do much without a precursor
        return false;

    BOOST_FOREACH(const Precursor& precursor, spectrum.precursors)
    {
        if (precursor.selectedIons.empty())
            continue;

        BOOST_FOREACH(const SelectedIon& si, precursor.selectedIons)
        BOOST_FOREACH(const CVParam& cvParam, si.cvParams)
        {
            switch (cvParam.cvid)
            {
                case MS_charge_state:
                case MS_possible_charge_state:
                    if (chargeStateSet_.contains(cvParam.valueAs<int>()))
                        return true;
                    break;

                default:
                    break;
            }
        }
    }

    // at this point the charge state could not be determined;
    // these spectra can be included/excluded by including 0 in the chargeStateSet
    return chargeStateSet_.contains(0);
}


//
// SpectrumList_FilterPredicate_PrecursorMzSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_PrecursorMzSet::SpectrumList_FilterPredicate_PrecursorMzSet(const std::set<double>& precursorMzSet, chemistry::MZTolerance tolerance, FilterMode mode, TargetMode target)
:   precursorMzSet_(precursorMzSet), tolerance_(tolerance), mode_(mode), target_(target)
{}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_PrecursorMzSet::accept(const msdata::Spectrum& spectrum) const
{
    double precursorMz = getPrecursorMz(spectrum);
    if (precursorMz == 0)
    {
        CVParam param = spectrum.cvParam(MS_ms_level);
        if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
        int msLevel = param.valueAs<int>();
        // If not level 1, then it should have a precursor, so request more meta data.
        if (msLevel != 1) return boost::logic::indeterminate;
    }

    auto lb = precursorMzSet_.lower_bound(precursorMz - tolerance_);
    auto ub = precursorMzSet_.lower_bound(precursorMz + tolerance_);
    bool found = (lb != ub) || (lb != precursorMzSet_.end() && *lb == precursorMz);
    if (mode_ == FilterMode_Include)
        return found;
    else
        return !found;
}

PWIZ_API_DECL double SpectrumList_FilterPredicate_PrecursorMzSet::getPrecursorMz(const msdata::Spectrum& spectrum) const
{
    for (size_t i = 0; i < spectrum.precursors.size(); i++)
    {
        switch (target_)
        {
            case TargetMode_Selected:
            {
                for (size_t j = 0; j < spectrum.precursors[i].selectedIons.size(); j++)
                {
                    CVParam param = spectrum.precursors[i].selectedIons[j].cvParam(MS_selected_ion_m_z);
                    if (param.cvid != CVID_Unknown)
                        return lexical_cast<double>(param.value);
                }
            }
            case TargetMode_Isolated:
            {
                CVParam param = spectrum.precursors[i].isolationWindow.cvParam(MS_isolation_window_target_m_z);
                if (param.cvid != CVID_Unknown)
                    return lexical_cast<double>(param.value);
            }
        }
    }
    return 0;
}


//
// SpectrumList_FilterPredicate_DefaultArrayLengthSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_DefaultArrayLengthSet::SpectrumList_FilterPredicate_DefaultArrayLengthSet(const IntegerSet& defaultArrayLengthSet)
:   defaultArrayLengthSet_(defaultArrayLengthSet)
{}


PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_DefaultArrayLengthSet::accept(const msdata::Spectrum& spectrum) const
{
    if (spectrum.defaultArrayLength == 0)
        return boost::logic::indeterminate;
    return defaultArrayLengthSet_.contains(spectrum.defaultArrayLength);
}


//
// SpectrumList_FilterPredicate_ActivationType
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ActivationType::SpectrumList_FilterPredicate_ActivationType(const set<CVID> cvFilterItems_, bool hasNoneOf_)
: hasNoneOf(hasNoneOf_)
{
    BOOST_FOREACH(const CVID cvid, cvFilterItems_)
    {
        CVTermInfo info = cvTermInfo(cvid); 
        if (!cvIsA(cvid, MS_dissociation_method))
            throw runtime_error("first argument not an activation type");

        cvFilterItems.insert(cvid);
    }

}

PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_ActivationType::accept(const msdata::Spectrum& spectrum) const
{
    CVParam param = spectrum.cvParamChild(MS_spectrum_type);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    if (!cvIsA(param.cvid, MS_mass_spectrum))
        return true; // activation filter doesn't affect non-MS spectra

    param = spectrum.cvParam(MS_ms_level);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    int msLevel = param.valueAs<int>();

    if (msLevel == 1)
        return true; // activation filter doesn't affect MS1 spectra

    if (spectrum.precursors.empty() ||
        spectrum.precursors[0].selectedIons.empty() ||
        spectrum.precursors[0].selectedIons[0].empty())
        return boost::logic::indeterminate;

    const Activation& activation = spectrum.precursors[0].activation;

    bool res = true;
    BOOST_FOREACH(const CVID cvid, cvFilterItems)
    {
        if (hasNoneOf)
            res &= !activation.hasCVParam(cvid);
        else
            res &= activation.hasCVParam(cvid);
    }

    return res;
}


//
// SpectrumList_FilterPredicate_AnalyzerType
//


PWIZ_API_DECL SpectrumList_FilterPredicate_AnalyzerType::SpectrumList_FilterPredicate_AnalyzerType(const set<CVID> cvFilterItems_)
{
    BOOST_FOREACH(const CVID cvid, cvFilterItems_)
    {
        CVTermInfo info = cvTermInfo(cvid); 
        if (std::find(info.parentsIsA.begin(), info.parentsIsA.end(), MS_mass_analyzer_type) == info.parentsIsA.end())
        {
            throw runtime_error("first argument not an analyzer type");
        }

        cvFilterItems.insert(cvid);
    }

}

PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_AnalyzerType::accept(const msdata::Spectrum& spectrum) const
{
    bool res = false;
    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];

    vector<CVID> massAnalyzerTypes;
    if (scan.instrumentConfigurationPtr.get())
        for (auto& component : scan.instrumentConfigurationPtr->componentList)
        {
            CVID massAnalyzerType = component.cvParamChild(MS_mass_analyzer_type).cvid;
            if (massAnalyzerType != CVID_Unknown)
                massAnalyzerTypes.push_back(massAnalyzerType);
        }

    if (massAnalyzerTypes.empty())
        return boost::logic::indeterminate;

    for(CVID cvid : cvFilterItems)
        for (CVID massAnalyzerType : massAnalyzerTypes)
            if (cvIsA(massAnalyzerType, cvid))
            {
            res = true;
            break;
        }

    return res;
}


//
// SpectrumList_FilterPredicate_Polarity
//


PWIZ_API_DECL SpectrumList_FilterPredicate_Polarity::SpectrumList_FilterPredicate_Polarity(CVID polarity) : polarity(polarity) {}

PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_Polarity::accept(const msdata::Spectrum& spectrum) const
{
    CVParam param = spectrum.cvParamChild(MS_scan_polarity);
    if (param.cvid == CVID_Unknown)
        return boost::logic::indeterminate;
    return param.cvid == polarity;
}


//
// SpectrumList_FilterPredicate_MzPresent
//

SpectrumList_FilterPredicate_MzPresent::SpectrumList_FilterPredicate_MzPresent(chemistry::MZTolerance mzt, std::set<double> mzSet, ThresholdFilter tf, FilterMode mode) : mzt_(mzt), mzSet_(mzSet), tf_(tf), mode_(mode) {}

boost::logic::tribool SpectrumList_FilterPredicate_MzPresent::accept(const msdata::Spectrum& spectrum) const
{
    if (spectrum.getMZArray().get() == NULL || spectrum.getIntensityArray().get() == NULL)
        return boost::logic::indeterminate;

    // threshold spectrum
    SpectrumPtr sptr(new Spectrum(spectrum));
    tf_(sptr);

    for (auto iterMZ = sptr->getMZArray()->data.begin(); iterMZ != sptr->getMZArray()->data.end(); ++iterMZ)
    {
        for (auto mzSetIter = mzSet_.begin(); mzSetIter != mzSet_.end(); ++mzSetIter) {
            if (isWithinTolerance(*mzSetIter, *iterMZ, mzt_))
            {
                if (mode_ == FilterMode_Exclude)
                    return false;
                else
                    return true;
            }
        }
    }

    if (mode_ == FilterMode_Exclude)
        return true;
    return false;
}

//
// SpectrumList_FilterPredicate_ThermoScanFilter
//

SpectrumList_FilterPredicate_ThermoScanFilter::SpectrumList_FilterPredicate_ThermoScanFilter(const string& matchString, bool matchExact, bool inverse) : matchString_(matchString), matchExact_(matchExact), inverse_(inverse) {}

boost::logic::tribool SpectrumList_FilterPredicate_ThermoScanFilter::accept(const msdata::Spectrum& spectrum) const
{
    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];
    CVParam param = scan.cvParam(MS_filter_string);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    string scanFilter = param.value;
    bool filterPass;
    if (matchExact_)
    {
        filterPass = scanFilter == matchString_;
    }
    else
    {
        filterPass = scanFilter.find(matchString_) != string::npos;
    }
    if (inverse_) {filterPass = !filterPass;}
    return filterPass;
}


//
// SpectrumList_FilterPredicate_CollisionEnergy
//


PWIZ_API_DECL SpectrumList_FilterPredicate_CollisionEnergy::SpectrumList_FilterPredicate_CollisionEnergy(double collisionEnergyLow, double collisionEnergyHigh, bool acceptNonCID, bool acceptMissingCE, FilterMode mode)
    : ceLow_(collisionEnergyLow), ceHigh_(collisionEnergyHigh), acceptNonCID_(acceptNonCID), acceptMissingCE_(acceptMissingCE), mode_(mode) {}

PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_CollisionEnergy::accept(const msdata::Spectrum& spectrum) const
{
    CVParam param = spectrum.cvParamChild(MS_spectrum_type);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    if (!cvIsA(param.cvid, MS_mass_spectrum))
        return true; // activation filter doesn't affect non-MS spectra

    param = spectrum.cvParam(MS_ms_level);
    if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;
    int msLevel = param.valueAs<int>();

    if (msLevel == 1)
        return true; // activation filter doesn't affect MS1 spectra

    if (spectrum.precursors.empty() ||
        spectrum.precursors[0].selectedIons.empty() ||
        spectrum.precursors[0].selectedIons[0].empty())
        return boost::logic::indeterminate;

    const Activation& activation = spectrum.precursors[0].activation;
    auto dissociationMethods = activation.cvParamChildren(MS_dissociation_method);
    if (dissociationMethods.empty())
        return boost::logic::indeterminate;

    bool hasCID = false;
    for (const auto& dm : dissociationMethods)
        if (cvIsA(dm.cvid, MS_collision_induced_dissociation))
        {
            hasCID = true;
            break;
        }
    if (!hasCID)
        return acceptNonCID_;

    // at this point if CE is missing, assume it won't be present at any DetailLevel
    CVParam ce = activation.cvParam(MS_collision_energy);
    if (ce.empty())
        return acceptMissingCE_;

    double ceValue = ce.valueAs<double>();
    if (ceValue >= ceLow_ && ceValue <= ceHigh_)
        return mode_ == FilterMode_Include;
    return mode_ == FilterMode_Exclude;
}

} // namespace analysis
} // namespace pwiz

