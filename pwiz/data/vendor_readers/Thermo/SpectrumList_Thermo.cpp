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


#include "SpectrumList_Thermo.hpp"

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/range/algorithm/count_if.hpp>
#include <boost/range/adaptor/reversed.hpp>
#include <thread>

using namespace pwiz::cv;

namespace {

    std::vector<double> getMultiFillTimes(const string& multiFill)
    {
        std::vector<double> fillTimes;
        if (multiFill.empty() || bal::trim_copy(multiFill).empty())
        {
            // This parameter is not specified, return an empty set of fill times
            return fillTimes;
        }

        vector<string> attribute;
        boost::split(attribute, multiFill, boost::is_any_of("="));
        if (attribute.size() != 2 || attribute[0].compare("IT") != 0)
            throw runtime_error("[SpectrumList_Thermo::getMultiFillTImes()] Unexpected fill time format: " + multiFill);

        string fillDataString = attribute[1];
        bal::trim_if(fillDataString, bal::is_any_of(" ,"));

        vector<string> fillTimeStrings;
        bal::split(fillTimeStrings, fillDataString, bal::is_any_of(","));
        for (const string& s : fillTimeStrings)
            fillTimes.push_back(boost::lexical_cast<double>(s));
        return fillTimes;
    }

    TEST_CASE("getMultiFillTimes()") {
        CHECK(getMultiFillTimes("") == vector<double>{});
        CHECK(getMultiFillTimes("IT=12.3,12.3") == vector<double>{12.3, 12.3});
        CHECK(getMultiFillTimes("IT=12.3,12.3,") == vector<double>{12.3, 12.3});
        CHECK(getMultiFillTimes("IT=12.3,12.3,  ") == vector<double>{12.3, 12.3});
    }
} // namespace


#ifdef PWIZ_READER_THERMO
#include "Reader_Thermo_Detail.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <boost/bind.hpp>
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Thermo;

SpectrumList_Thermo::SpectrumList_Thermo(const MSData& msd, RawFilePtr rawfile, const Reader::Config& config)
:   msd_(msd), rawfile_(rawfile), config_(config),
    size_(0),
    precursorCache_(MemoryMRUCacheMode_MetaDataAndBinaryData, 10)
{
    createIndex();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Thermo::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Thermo::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Thermo::find(const string& id) const
{
    bool success;
    size_t scanNumber = lexical_cast<size_t>(id, success);
    if (success && scanNumber>=1 && scanNumber<=size())
        return scanNumber-1;
    else
    {
        map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
        if (scanItr == idToIndexMap_.end())
            return checkNativeIdFindResult(size_, id);
        return scanItr->second;
    }

    return checkNativeIdFindResult(size_, id);
}


InstrumentConfigurationPtr SpectrumList_Thermo::findInstrumentConfiguration(const MSData& msd, CVID massAnalyzerType) const
{
    if (msd.instrumentConfigurationPtrs.empty())
        return InstrumentConfigurationPtr();

    for (const auto& icPtr : msd.instrumentConfigurationPtrs)
    {
        size_t analyzerCount = boost::range::count_if(icPtr->componentList, [](const auto& component) { return component.type == ComponentType_Analyzer; });

        try
        {
            if (icPtr->componentList.analyzer(analyzerCount - 1).hasCVParam(massAnalyzerType))
                return icPtr;
        }
        catch (out_of_range&)
        {
            continue;
        }
    }

    warn_once(("no matching instrument configuration for analyzer type " + cvTermInfo(massAnalyzerType).shortName()).c_str());
    return InstrumentConfigurationPtr();
}

inline boost::optional<double> getElectronvoltActivationEnergy(const ScanInfo& scanInfo)
{
    if (scanInfo.activationType() & ActivationType_HCD)
    {
        try
        {
            return scanInfo.trailerExtraValueDouble("HCD Energy eV:");
        }
        catch (RawEgg&)
        {
            try
            {
                string ftMessage = scanInfo.trailerExtraValue("FT Analyzer Message:");
                size_t hcdIndex = ftMessage.find("HCD=");
                if (hcdIndex != string::npos)
                {
                    hcdIndex += 4;
                    return lexical_cast<double>(ftMessage.substr(hcdIndex, ftMessage.find_first_not_of("0123456789", hcdIndex) - hcdIndex));
                }
            }
            catch (RawEgg&)
            {}
            return scanInfo.precursorActivationEnergy(0);
        }
    }
    else if (scanInfo.activationType() & ActivationType_CID)
        return scanInfo.precursorActivationEnergy(0);
    else
        return boost::optional<double>();
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, DetailLevel detailLevel) const 
{
    return spectrum(index, detailLevel, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const 
{
    //boost::lock_guard<boost::recursive_mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)

    if (index >= size_)
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Bad index: " 
                            + lexical_cast<string>(index));

    const IndexEntry& ie = index_[index];

    auto raw = rawfile_->getRawByThread(std::hash<std::thread::id>()(std::this_thread::get_id()));

    try
    {
        raw->setCurrentController(ie.controllerType, ie.controllerNumber);
    }
    catch (RawEgg& r)
    {
        const char *what = r.what();
        if (what == NULL)
            what = "cause unknown";
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error setting controller to: " +
                            lexical_cast<string>(ie.controllerType) + "," +
                            lexical_cast<string>(ie.controllerNumber) +
                            " (" + what + ")");
    }

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Allocation error.");

    result->index = index;
    result->id = ie.id;

    ScanType scanType = ie.scanType;

    int msLevel = (int) ie.msOrder;

    if (ie.controllerType == Controller_MS)
    {
        switch (scanType)
        {
            case ScanType_SIM: result->set(MS_SIM_spectrum); break;
            case ScanType_SRM: result->set(MS_SRM_spectrum); break;
            default:
                switch (ie.msOrder)
                {
                    case MSOrder_NeutralLoss:   result->set(MS_constant_neutral_loss_spectrum); msLevel = 2; break;
                    case MSOrder_NeutralGain:   result->set(MS_constant_neutral_gain_spectrum); msLevel = 2; break;
                    case MSOrder_ParentScan:    result->set(MS_precursor_ion_spectrum); msLevel = 2; break;
                    case MSOrder_MS:            result->set(MS_MS1_spectrum); break;
                    default:                    result->set(MS_MSn_spectrum); break;
                }
                break;
        }
        result->set(MS_ms_level, msLevel);
    }
    else
        result->set(MS_EMR_spectrum);

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.set(MS_scan_start_time, raw->rt(ie.scan), UO_minute);

    // Parsing scanInfo is not instant.
    if (detailLevel == DetailLevel_InstantMetadata)
        return result;

    // Revert to previous behavior for getting binary data or not.
    bool getBinaryData = (detailLevel == DetailLevel_FullData);

    ScanInfoPtr scanInfo;
    try
    {
        // get rawfile::ScanInfo and translate
        scanInfo = raw->getScanInfo(ie.scan);
        if (!scanInfo.get())
            throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo.");
    }
    catch (RawEgg& e)
    {
        throw runtime_error(string("[SpectrumList_Thermo::spectrum()] Error retrieving ScanInfo: ") + e.what());
    }

    try
    {
        if (scanInfo->isConstantNeutralLoss())
        {
            scan.set(MS_analyzer_scan_offset, scanInfo->analyzerScanOffset(), MS_m_z);
        }

        // special handling for non-MS scans
        if (ie.controllerType != Controller_MS)
        {
            scan.instrumentConfigurationPtr = msd_.instrumentConfigurationPtrs.back();

            if (scanInfo->basePeakMass() > 0)
            {
                result->set(MS_base_peak_m_z, scanInfo->basePeakMass(), UO_nanometer);
                result->set(MS_base_peak_intensity, scanInfo->basePeakIntensity());
            }
            result->set(MS_total_ion_current, scanInfo->totalIonCurrent());

            scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass(), UO_nanometer));

            MassListPtr xyList = raw->getMassList(ie.scan, "", Cutoff_None, 0, 0, false);
            result->defaultArrayLength = xyList->size();

            if (xyList->size() > 0)
            {
                result->set(MS_lowest_observed_wavelength, xyList->mzArray.front(), UO_nanometer);
                result->set(MS_highest_observed_wavelength, xyList->mzArray.back(), UO_nanometer);
            }

            if (getBinaryData)
            {
                result->swapMZIntensityArrays(xyList->mzArray, xyList->intensityArray, MS_number_of_detector_counts);

                // replace "m/z array" term with "wavelength array"
                BinaryDataArray& xArray = *result->getMZArray();
                vector<CVParam>::iterator itr = std::find_if(xArray.cvParams.begin(),
                                                             xArray.cvParams.end(),
                                                             CVParamIs(MS_m_z_array));
                *itr = CVParam(MS_wavelength_array, "", UO_nanometer);
            }
            return result;
        }

        // MS scan metadata beyond this point
        if (scanInfo->ionizationType() == IonizationType_MALDI)
        {
            result->spotID += scanInfo->trailerExtraValue("Sample Position:");
            result->spotID += "," + scanInfo->trailerExtraValue("Fine Position:");
            result->spotID += "," + scanInfo->trailerExtraValue("Absolute X Position:");
            result->spotID += "x" + scanInfo->trailerExtraValue("Absolute Y Position:");
        }

        MassAnalyzerType analyzerType = scanInfo->massAnalyzerType();
        if (ie.controllerType == Controller_MS)
            scan.instrumentConfigurationPtr = findInstrumentConfiguration(msd_, translate(analyzerType));

        string filterString = scanInfo->filter();
        scan.set(MS_filter_string, filterString);

        string scanDescriptionStr = scanInfo->trailerExtraValue("Scan Description:");
        bal::trim(scanDescriptionStr);
        if (!scanDescriptionStr.empty())
            result->userParams.emplace_back("scan description", scanDescriptionStr, "xsd:string");

        int scanSegment = 1, scanEvent = 1;

        string scanSegmentStr = scanInfo->trailerExtraValue("Scan Segment:");
        if (!scanSegmentStr.empty())
            scanSegment = lexical_cast<int>(scanSegmentStr);

        string scanEventStr = scanInfo->trailerExtraValue("Scan Event:");
        if (!scanEventStr.empty())
        {
            scan.set(MS_preset_scan_configuration, scanEventStr);
            scanEvent = lexical_cast<int>(scanEventStr);
        }

        if (scanType == ScanType_Zoom || scanInfo->isEnhanced())
            result->set(MS_enhanced_resolution_scan);

        PolarityType polarityType = scanInfo->polarityType();
        if (polarityType!=PolarityType_Unknown) result->set(translate(polarityType));

        bool doCentroid = msLevelsToCentroid.contains(msLevel);

        if (scanInfo->isProfileScan() && !doCentroid)
        {
            result->set(MS_profile_spectrum);
        }
        else
        {
            result->set(MS_centroid_spectrum); 
            doCentroid = scanInfo->isProfileScan();
        }

        result->set(MS_base_peak_m_z, scanInfo->basePeakMass(), MS_m_z);
        result->set(MS_base_peak_intensity, scanInfo->basePeakIntensity(), MS_number_of_detector_counts);
        result->set(MS_total_ion_current, scanInfo->totalIonCurrent());

        if (scanInfo->FAIMSOn())
            result->set(MS_FAIMS_compensation_voltage, scanInfo->compensationVoltage());

        size_t scanRangeCount = scanInfo->scanRangeCount();
        if ((scanType == ScanType_SIM || scanType == ScanType_SRM) && scanRangeCount > 1)
        {
            for (size_t i=0; i < scanRangeCount; ++i)
            {
                const pair<double, double>& scanRange = scanInfo->scanRange(i);
                scan.scanWindows.push_back(ScanWindow(scanRange.first, scanRange.second, MS_m_z));
            }
        }
        else
            scan.scanWindows.push_back(ScanWindow(scanInfo->lowMass(), scanInfo->highMass(), MS_m_z));

        if (msLevel > 1)
        {
            try
            {
                double mzMonoisotopic = scanInfo->trailerExtraValueDouble("Monoisotopic M/Z:");
                scan.userParams.push_back(UserParam("[Thermo Trailer Extra]Monoisotopic M/Z:", 
                                                    lexical_cast<string>(mzMonoisotopic),
                                                    "xsd:float"));
            }
            catch (RawEgg&)
            {
            }
        }

        try
        {
            double injectionTime = scanInfo->trailerExtraValueDouble("Ion Injection Time (ms):");
            scan.set(MS_ion_injection_time, injectionTime, UO_millisecond);

            /* TODO: add verbosity settings to Reader interface
            if (analyzerType == MassAnalyzerType_Orbitrap || analyzerType == MassAnalyzerType_FTICR)
            {
                string ftSettings = scanInfo->trailerExtraValue("FT Analyzer Settings:");
                string ftMessage = scanInfo->trailerExtraValue("FT Analyzer Message:");
                scan.userParams.push_back(UserParam("FT Analyzer Settings", ftSettings, "xsd:string"));
                scan.userParams.push_back(UserParam("FT Analyzer Message", ftMessage, "xsd:string"));
                string msResolution = scanInfo->trailerExtraValue("FT Resolution:");
                scan.cvParams.push_back(CVParam(MS_mass_resolution, msResolution));
            }*/
        }
        catch (RawEgg&)
        {
        }

        long precursorCount = scanInfo->precursorCount();
        // validate that dependent scans have as many precursors as their ms level minus one
        if (msLevel != -1 &&
            scanInfo->isDependent() &&
            !scanInfo->hasMultiplePrecursors() &&
            precursorCount != msLevel-1)
        {
            throw runtime_error("precursor count does not match ms level");
        }

        const auto& precursorInfoList = scanInfo->precursorInfo();

        if (scanInfo->hasMultiplePrecursors())
        {
            vector<double> isolationWidths = scanInfo->getIsolationWidths();
            if (precursorCount != (long) isolationWidths.size())
            {
                throw runtime_error("precursor count does not match isolation width count");
            }

            // check if multiple fill data exists for this scan
            vector<double> fillTimes = getMultiFillTimes(scanInfo->trailerExtraValue("Multi Inject Info:"));
            bool addMultiFill = !fillTimes.empty() && fillTimes.size() == (size_t) precursorCount;
            auto fillTimeItr = addMultiFill ? fillTimes.begin() : fillTimes.end();

            Precursor precursor;
            SelectedIon selectedIon;

            //boost::optional<double> electronvoltActivationEnergy = getElectronvoltActivationEnergy(*scanInfo); 

            for (const auto& precursorInfo : boost::adaptors::reverse(precursorInfoList)) // highest ms level first
            {
                precursor.clear();
                precursor.isolationWindow.clear();
                selectedIon.clear();

                double isolationMz = precursorInfo.isolationMZ;
                double isolationWidth = precursorInfo.isolationWidth / 2;

                // trailer extra isolation width overrides API one for SPS (which isn't always accurate for some reason)
                // but not MSX because it has some crazy Isolation Width trailer values
                if (scanInfo->isSPS())
                {
                    // CONSIDER: is checking MS2 Isolation Width for MS3 SPS spectra the correct thing to do?
                    string isolationWidthTag = "MS" + lexical_cast<string>(msLevel-1) + " Isolation Width:";
                    isolationWidth = max(isolationWidth, scanInfo->trailerExtraValueDouble(isolationWidthTag) / 2);
                }

                precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth, MS_m_z);
                precursor.isolationWindow.userParams.emplace_back("ms level", lexical_cast<string>(precursorInfo.msLevel));

                ActivationType activationType = scanInfo->activationType();
                if (activationType == ActivationType_Unknown)
                    activationType = ActivationType_CID; // assume CID

                setActivationType(activationType, scanInfo->supplementalActivationType(), precursor.activation);

                // TODO: replace with commented out code below after mailing list discussion
                if ((activationType & ActivationType_CID) || (activationType & ActivationType_HCD))
                     precursor.activation.set(MS_collision_energy, precursorInfo.activationEnergy, UO_electronvolt);

                if (scanInfo->supplementalActivationType() != ActivationType_Unknown)
                    precursor.activation.set(MS_supplemental_collision_energy, scanInfo->supplementalActivationEnergy(), UO_electronvolt);

                //if (i == 0 && electronvoltActivationEnergy)
                //    precursor.activation.set(MS_collision_energy, electronvoltActivationEnergy.get(), UO_electronvolt);
                //else
                //    precursor.activation.set(MS_collision_energy, scanInfo->precursorActivationEnergy(i), UO_electronvolt);

                selectedIon.set(MS_selected_ion_m_z, isolationMz, MS_m_z);
                precursor.selectedIons.clear();
                precursor.selectedIons.push_back(selectedIon);

                double precursorIsolationMz = 0; // TODO: how to get precursor's isolation m/z in this branch? (i.e. the isolation m/z of the MS2 for an MS3 spectrum)
                size_t precursorScanIndex = findPrecursorSpectrumIndex(raw, precursorInfo.msLevel, isolationMz, precursorIsolationMz, index);
                if (precursorScanIndex < index_.size())
                    precursor.spectrumID = index_[precursorScanIndex].id;

                if (addMultiFill)
                {
                    scan.userParams.push_back(UserParam("MultiFillTime", lexical_cast<string>(*fillTimeItr), "xsd:double"));
                    ++fillTimeItr;
                }
                result->precursors.push_back(precursor);
            }
        }
        else if (precursorCount > 0)
        {
            // in this branch, each precursor should be from a different ms level (SPS and MSX spectra handled by hasMultiplePrecursors()) 
            for (int i = precursorCount - 1; i >= 0; --i) // highest ms level first
            {
                // Note: we report what RawFile gives us, which comes from the filter string, the API, and the trailer extra values;
                // if desired, precursor recalculation is done outside the Reader.

                Precursor precursor;
                Product product;
                SelectedIon selectedIon;

                const double defaultIsolationWindowLowerOffset = 1.5;
                const double defaultIsolationWindowUpperOffset = 2.5;

                const auto& precursorInfo = precursorInfoList[i];

                if (precursorInfo.msLevel == msLevel - 1)
                {
                    // isolationWindow

                    double isolationWidth = precursorInfo.isolationWidth / 2;

                    try
                    {
                        string isolationWidthTag = "MS" + lexical_cast<string>(msLevel) + " Isolation Width:";
                        isolationWidth = scanInfo->trailerExtraValueDouble(isolationWidthTag) / 2;
                    }
                    catch (RawEgg&)
                    {}

                    // if scan trailer did not have isolation width, try the instrument method
                    if (isolationWidth == 0)
                    {
                        isolationWidth = raw->getIsolationWidth(scanSegment, scanEvent) / 2;
                        if (isolationWidth == 0)
                            isolationWidth = raw->getDefaultIsolationWidth(scanSegment, msLevel) / 2;
                    }

                    double isolationMz = ie.isolationMz;

                    if (ie.msOrder == MSOrder_ParentScan) // precursor ion scan
                    {
                        product.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                        if (isolationWidth != 0)
                        {
                            product.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth, MS_m_z);
                            product.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth, MS_m_z);
                        }
                    }
                    else
                    {
                        precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                        if (isolationWidth != 0)
                        {
                            precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth, MS_m_z);
                            precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth, MS_m_z);
                        }
                    }

                    double selectedIonMz = scanInfo->precursorMZ(i); // monoisotopic m/z is preferred
                    if (selectedIonMz > 0 && detailLevel != DetailLevel_FastMetadata)
                    {
                        long precursorCharge = scanInfo->precursorCharge();

                        // if an appropriate zoom scan was found, try to get monoisotopic m/z and/or charge from it
                        ScanInfoPtr zoomScanInfo = findPrecursorZoomScan(raw, msLevel - 1, isolationMz, index);
                        if (zoomScanInfo.get())
                        {
                            if (selectedIonMz == isolationMz)
                            {
                                try
                                {
                                    double monoisotopicMz = zoomScanInfo->trailerExtraValueDouble("Monoisotopic M/Z:");
                                    if (monoisotopicMz > 0)
                                        selectedIonMz = monoisotopicMz;
                                }
                                catch (RawEgg&)
                                {}
                            }

                            if (precursorCharge == 0)
                            {
                                try
                                {
                                    precursorCharge = zoomScanInfo->trailerExtraValueLong("Charge State:");
                                }
                                catch (RawEgg&)
                                {}
                            }
                        }

                        // if the monoisotopic m/z is outside the isolation window (due to Thermo firmware bug), reset it to isolation m/z
                        if (isolationWidth <= 2.0)
                        {
                            if ((selectedIonMz < (isolationMz - defaultIsolationWindowLowerOffset * 2)) || (selectedIonMz > (isolationMz + defaultIsolationWindowUpperOffset)))
                                selectedIonMz = isolationMz;
                        }
                        else if ((selectedIonMz < (isolationMz - isolationWidth)) || (selectedIonMz > (isolationMz + isolationWidth)))
                            selectedIonMz = isolationMz;

                        // add selected ion m/z (even if it's still equal to isolation m/z)
                        selectedIon.set(MS_selected_ion_m_z, selectedIonMz, MS_m_z);

                        // add charge state if available
                        if (precursorCharge > 0)
                            selectedIon.set(MS_charge_state, precursorCharge);

                        // TODO: check "Master Scan Number:"

                        // find the precursor scan, which is the previous scan with the current scan's msLevel-1 and, if
                        // the current scan is MS3 or higher, its precursor scan's last isolation m/z should be the next
                        // to last isolation m/z of the current scan;
                        // i.e. MS3 with filter "234.56@cid30.00 123.45@cid30.00" matches to MS2 with filter "234.56@cid30.00"
                        double precursorIsolationMz = i > 0 ? scanInfo->precursorMZ(i - 1, false) : 0;
                        size_t precursorScanIndex = findPrecursorSpectrumIndex(raw, msLevel - 1, isolationMz, precursorIsolationMz, index);
                        if (precursorScanIndex < index_.size())
                        {
                            precursor.spectrumID = index_[precursorScanIndex].id;

                            if (detailLevel >= DetailLevel_FullMetadata)
                            {
                                double isolationQueryWidth = isolationWidth == 0 ? isolationWidth : defaultIsolationWindowLowerOffset;
                                double precursorIntensity = getPrecursorIntensity(precursorScanIndex, isolationMz, isolationQueryWidth, msLevelsToCentroid);
                                if (precursorIntensity > 0)
                                    selectedIon.set(MS_peak_intensity, precursorIntensity, MS_number_of_detector_counts);
                            }
                        }
                    }

                    ActivationType activationType = scanInfo->activationType();
                    if (activationType == ActivationType_Unknown)
                        activationType = ActivationType_CID; // assume CID
                    setActivationType(activationType, scanInfo->supplementalActivationType(), precursor.activation);

                    // TODO: replace with commented out code below after mailing list discussion
                    if ((activationType & ActivationType_CID) || (activationType & ActivationType_HCD))
                        precursor.activation.set(MS_collision_energy, scanInfo->precursorActivationEnergy(i), UO_electronvolt);

                    if (scanInfo->supplementalActivationType() != ActivationType_Unknown)
                        precursor.activation.set(MS_supplemental_collision_energy, scanInfo->supplementalActivationEnergy(), UO_electronvolt);

                    //if (electronvoltActivationEnergy)
                    //    precursor.activation.set(MS_collision_energy, electronvoltActivationEnergy.get(), UO_electronvolt);

                    if (ie.msOrder != MSOrder_ParentScan)
                        precursor.selectedIons.push_back(selectedIon);

                    precursor.isolationWindow.userParams.emplace_back("ms level", lexical_cast<string>(precursorInfo.msLevel));
                    result->precursors.push_back(precursor);

                    if (ie.msOrder == MSOrder_ParentScan)
                        result->products.push_back(product);
                }
                else // this is not the primary precursor (precursorMsLevel < msLevel-1)
                {
                    // isolationWindow
                    double isolationWidth = precursorInfo.isolationWidth / 2;

                    // try the instrument method
                    if (isolationWidth == 0)
                    {
                        isolationWidth = raw->getIsolationWidth(scanSegment, scanEvent) / 2;
                        if (isolationWidth == 0)
                            isolationWidth = raw->getDefaultIsolationWidth(scanSegment, precursorInfo.msLevel) / 2;
                    }

                    double isolationMz = precursorInfo.isolationMZ;

                    precursor.isolationWindow.set(MS_isolation_window_target_m_z, isolationMz, MS_m_z);
                    if (isolationWidth != 0)
                    {
                        precursor.isolationWindow.set(MS_isolation_window_lower_offset, isolationWidth, MS_m_z);
                        precursor.isolationWindow.set(MS_isolation_window_upper_offset, isolationWidth, MS_m_z);
                    }

                    double selectedIonMz = precursorInfo.monoisotopicMZ; // probably the same as isolationMz

                    // if the monoisotopic m/z is outside the isolation window (due to Thermo firmware bug), reset it to isolation m/z
                    if (isolationWidth <= 2.0)
                    {
                        if ((selectedIonMz < (isolationMz - defaultIsolationWindowLowerOffset * 2)) || (selectedIonMz > (isolationMz + defaultIsolationWindowUpperOffset)))
                            selectedIonMz = isolationMz;
                    }
                    else if ((selectedIonMz < (isolationMz - isolationWidth)) || (selectedIonMz > (isolationMz + isolationWidth)))
                        selectedIonMz = isolationMz;

                    // add selected ion m/z (even if it's still equal to isolation m/z)
                    selectedIon.set(MS_selected_ion_m_z, selectedIonMz, MS_m_z);

                    // add charge state if available
                    if (precursorInfo.chargeState > 0)
                        selectedIon.set(MS_charge_state, precursorInfo.chargeState);

                    ActivationType activationType = precursorInfo.activationType;
                    if (activationType == ActivationType_Unknown)
                        activationType = ActivationType_CID; // assume CID
                    setActivationType(activationType, ActivationType_Unknown, precursor.activation);

                    // TODO: replace with commented out code below after mailing list discussion
                    if ((activationType & ActivationType_CID) || (activationType & ActivationType_HCD))
                        precursor.activation.set(MS_collision_energy, precursorInfo.activationEnergy, UO_electronvolt);

                    //if (electronvoltActivationEnergy)
                    //    precursor.activation.set(MS_collision_energy, electronvoltActivationEnergy.get(), UO_electronvolt);

                    precursor.selectedIons.push_back(selectedIon);
                    precursor.isolationWindow.userParams.emplace_back("ms level", lexical_cast<string>(precursorInfo.msLevel));
                    result->precursors.push_back(precursor);
                }
            }
        }

        if (detailLevel >= DetailLevel_FullMetadata)
        {
            MassListPtr massList = raw->getMassList(ie.scan, "", Cutoff_None, 0, 0, doCentroid);
            if (doCentroid)
                result->set(MS_profile_spectrum); // let SpectrumList_PeakPicker know this was a profile spectrum

            result->defaultArrayLength = massList->size();

            if (massList->size() > 0)
            {
                result->set(MS_lowest_observed_m_z, massList->mzArray.front(), MS_m_z);
                result->set(MS_highest_observed_m_z, massList->mzArray.back(), MS_m_z);
            }

            if (getBinaryData || msLevel < maxMsLevel_)
            {
                result->swapMZIntensityArrays(massList->mzArray, massList->intensityArray, MS_number_of_detector_counts);
            }

            if (msLevel < maxMsLevel_)
            {
                // insert into cache if there is a higher ms level
                // NB: even FullMetadata level will have binary arrays (because they have to be retrieved to get defaultArrayLength)
                boost::lock_guard<boost::mutex> lock(readMutex);
                precursorCache_.insert(CacheEntry(index, make_pair(result->getMZArray()->data, result->getIntensityArray()->data)));
            }
        }

        return result;
    }
    catch (RawEgg& e)
    {
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving spectrum \"" + result->id + "\": " + e.what());
    }
    catch (exception& e)
    {
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Error retrieving spectrum \"" + result->id + "\": " + e.what());
    }
    catch (...)
    {
        throw runtime_error("[SpectrumList_Thermo::spectrum()] Unknown exception retrieving spectrum \"" + result->id + "\"");
    }
}

PWIZ_API_DECL bool SpectrumList_Thermo::hasIonMobility() const
{
    return true; // May have FAIMS data - too expensive to go and actually check
}

PWIZ_API_DECL bool SpectrumList_Thermo::canConvertIonMobilityAndCCS() const
{
    return false;
}

PWIZ_API_DECL bool SpectrumList_Thermo::hasCombinedIonMobility() const
{
    return false;
}

PWIZ_API_DECL double SpectrumList_Thermo::ionMobilityToCCS(double ionMobility, double mz, int charge) const
{
    return 0;
}

PWIZ_API_DECL double SpectrumList_Thermo::ccsToIonMobility(double ccs, double mz, int charge) const
{
    return 0;
}

PWIZ_API_DECL int SpectrumList_Thermo::numSpectraOfScanType(ScanType scanType) const
{
    return spectraByScanType[(size_t) scanType];
}

PWIZ_API_DECL int SpectrumList_Thermo::numSpectraOfMSOrder(MSOrder msOrder) const
{
    // the +3 offset is because MSOrder_NeutralLoss == -3
    return spectraByMSOrder[(size_t) msOrder+3];
}

PWIZ_API_DECL void SpectrumList_Thermo::createIndex()
{
    using namespace boost::spirit::karma;

    spectraByScanType.resize(ScanType_Count, 0);
    spectraByMSOrder.resize(MSOrder_Count+3, 0); // can't use negative index and a std::map would be inefficient

    // calculate total spectra count from all controllers
    for (int controllerType = Controller_MS;
         controllerType < Controller_Count;
         ++controllerType)
    {
        // some controllers don't have spectra (even if they have a NumSpectra value!)
        if (controllerType == Controller_Analog ||
            controllerType == Controller_ADCard ||
            controllerType == Controller_UV)
            continue;

        long numControllers = rawfile_->getNumberOfControllersOfType((ControllerType) controllerType);

        if (controllerType == Controller_MS && numControllers > 1)
            throw runtime_error("[SpectrumList_Thermo::createIndex] Unable to handle RAW files with multiple MS controllers, please contact ProteoWizard support!");

        for (long n=1; n <= numControllers; ++n)
        {
            try
            {
                rawfile_->setCurrentController((ControllerType) controllerType, n);
            }
            catch (exception& e)
            {
                warn_once(("[SpectrumList_Thermo::createIndex] error setting controller to " + lexical_cast<std::string>(ControllerTypeStrings[controllerType]) + ": " + e.what()).c_str());
                continue;
            }

            long numSpectra = rawfile_->getLastScanNumber();
            switch (controllerType)
            {
                case Controller_MS:
                {
                    for (long scan=1; scan <= numSpectra; ++scan)
                    {
                        MSOrder msOrder = rawfile_->getMSOrder(scan);
                        if (config_.preferOnlyMsLevel > 0 && config_.preferOnlyMsLevel != (int)msOrder)
                            continue;

                        ScanType scanType = rawfile_->getScanType(scan);

                        // the +3 offset is because MSOrder_NeutralLoss == -3
                        ++spectraByMSOrder[msOrder+3];
                        ++spectraByScanType[scanType];
                        maxMsLevel_ = max(maxMsLevel_, msOrder+3);

                        switch (scanType)
                        {
                            // skip chromatogram-centric scan types
                            case ScanType_SIM:
                                if (config_.simAsSpectra)
                                    break;  // break out of switch (scanType)
                                continue;
                            case ScanType_SRM:
                                if (config_.srmAsSpectra)
                                    break;  // break out of switch (scanType)
                                continue;
                        }

                        index_.push_back(IndexEntry());
                        IndexEntry& ie = index_.back();
                        ie.controllerType = (ControllerType) controllerType;
                        ie.controllerNumber = n;
                        ie.scan = scan;
                        ie.index = index_.size()-1;

                        std::back_insert_iterator<std::string> sink(ie.id);
                        generate(sink,
                                 "controllerType=" << int_ << " controllerNumber=" << int_ << " scan=" << int_,
                                 (int) ie.controllerType, ie.controllerNumber, ie.scan);
                        idToIndexMap_[ie.id] = ie.index;

                        ie.scanType = scanType;
                        ie.msOrder = msOrder;
                        ie.isolationMz = msOrder > MSOrder_MS ? rawfile_->getPrecursorMass(scan, msOrder) : 0;
                    }
                }
                break;

                case Controller_PDA:
                {
                    if (config_.preferOnlyMsLevel > 0)
                        continue;

                    for (long scan=1; scan <= numSpectra; ++scan)
                    {
                        index_.push_back(IndexEntry());
                        IndexEntry& ie = index_.back();
                        ie.controllerType = (ControllerType) controllerType;
                        ie.controllerNumber = n;
                        ie.scan = scan;
                        ie.index = index_.size()-1;

                        std::back_insert_iterator<std::string> sink(ie.id);
                        generate(sink,
                                 "controllerType=" << int_ << " controllerNumber=" << int_ << " scan=" << int_,
                                 (int) ie.controllerType, ie.controllerNumber, ie.scan);
                        idToIndexMap_[ie.id] = ie.index;
                    }
                }
                break;

                default: break;
            }
        }
    }

    size_ = index_.size();
}


PWIZ_API_DECL size_t SpectrumList_Thermo::findPrecursorSpectrumIndex(RawFile* raw, int precursorMsLevel, double isolationMz, double precursorIsolationMz, size_t index) const
{
    // exit early if the precursor MS level doesn't exist (i.e. targeted MSn runs)
    if (numSpectraOfMSOrder(static_cast<MSOrder>(precursorMsLevel)) == 0)
        return size_;

    long masterScan = raw->getTrailerExtraValueLong(index_[index].scan, "Master Scan Number:", -1);

    // return first scan with MSn-1 that matches the precursor isolation m/z

    while (index > 0)
    {
        --index;
        const IndexEntry& ie = index_[index];
        if (ie.msOrder < MSOrder_MS)
            continue;

        if (masterScan > -1)
        {
            if (masterScan == ie.scan)
                return index;

            // master scan not in index (i.e. SIM scan without simAsSpectra)
            if (masterScan > ie.scan)
                return size_;
            continue;
        }

        if (static_cast<int>(ie.msOrder) == precursorMsLevel &&
            (precursorIsolationMz == 0 ||
             precursorIsolationMz == ie.isolationMz))
        {
            // make sure the precursorIsolationMz (for zoom scans) or isolationMz (for other MSn scans) is in the scan window
            double isolationMzToFind = ie.scanType == ScanType_Zoom ? precursorIsolationMz : isolationMz;
            bool mzInRange = false;
            auto scanInfo = raw->getScanInfo(ie.scan);
            size_t scanRangeCount = scanInfo->scanRangeCount();
            if ((ie.scanType == ScanType_SIM || ie.scanType == ScanType_SRM) && scanRangeCount > 1)
            {
                for (size_t i = 0; i < scanRangeCount && !mzInRange; ++i)
                {
                    const pair<double, double>& scanRange = scanInfo->scanRange(i);
                    mzInRange = isolationMzToFind >= scanRange.first && isolationMzToFind <= scanRange.second;
                }
            }
            else
            {
                mzInRange = isolationMzToFind >= scanInfo->lowMass() && isolationMzToFind <= scanInfo->highMass();
            }

            if (!mzInRange)
                continue;

            return index;
        }
    }

    return size_;
}


PWIZ_API_DECL double SpectrumList_Thermo::getPrecursorIntensity(int precursorSpectrumIndex, double isolationMz, double isolationHalfWidth, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    const PrecursorBinaryData* precursorBinaryData = nullptr;

    boost::lock_guard<boost::mutex> lock(readMutex);
    auto findItr = precursorCache_.find(precursorSpectrumIndex);
    if (findItr != precursorCache_.end())
        precursorBinaryData = &findItr->binaryData;

    // CONSIDER: is it worth it to keep separate caches for centroid and profile spectra?
    if (!precursorBinaryData)
    {
        const IndexEntry& precursorIndexEntry = index_[precursorSpectrumIndex];
        auto raw = rawfile_->getRawByThread(std::hash<std::thread::id>()(std::this_thread::get_id()));
        bool doCentroid = msLevelsToCentroid.contains((int)precursorIndexEntry.msOrder);
        MassListPtr massList = raw->getMassList(precursorIndexEntry.scan, "", Cutoff_None, 0, 0, doCentroid);
        precursorCache_.insert(CacheEntry(precursorSpectrumIndex, make_pair(massList->mzArray, massList->intensityArray)));
        precursorBinaryData = &precursorCache_.find(precursorSpectrumIndex)->binaryData;
    }

    const auto& mz = precursorBinaryData->first;
    const auto& intensity = precursorBinaryData->second;

    auto mzItr = lower_bound(mz.begin(), mz.end(), isolationMz - isolationHalfWidth);

    double precursorIntensity = 0;
    auto intensityItr = intensity.begin() + (mzItr - mz.begin());
    while (mzItr != mz.end() && *mzItr < isolationMz + isolationHalfWidth)
    {
        precursorIntensity += *intensityItr;
        ++mzItr;
        ++intensityItr;
    }
    return precursorIntensity;
}


/*
    This function tries to find any preceeding zoom scans that may be
    present for the current scan. This function is useful in getting 
    the precursor monoisotopic m/z and charge state information from
    the zoom scans, when the instrument is run in a triple-play mode.
*/
PWIZ_API_DECL ScanInfoPtr SpectrumList_Thermo::findPrecursorZoomScan(RawFile* raw, int precursorMsLevel, double precursorIsolationMz, size_t index) const
{
    // exit early if the precursor MS level doesn't exist (i.e. targeted MSn runs) OR no zoom scans exist
    if (numSpectraOfScanType(ScanType_Zoom) == 0)
        return ScanInfoPtr();

    // return first zoom scan with MSn-1 that contains the precursor isolation m/z

    while(index > 0)
    {
        --index;
        const IndexEntry& ie = index_[index];
        if (ie.scanType != ScanType_Zoom || static_cast<int>(ie.msOrder) != precursorMsLevel)
            continue;

        // Get the scan info and check if the precursor mass of this
        // MSn scan is with in the window of the zoom scan
        ScanInfoPtr zoomScanInfo = raw->getScanInfo(index+1);
        if (precursorIsolationMz < zoomScanInfo->lowMass() ||
            precursorIsolationMz > zoomScanInfo->highMass())
            continue;

        return zoomScanInfo;
    }

    return ScanInfoPtr();
}


} // detail
} // msdata
} // pwiz


#else // PWIZ_READER_THERMO

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_Thermo::size() const {return 0;}
const SpectrumIdentity& SpectrumList_Thermo::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_Thermo::find(const std::string& id) const {return 0;}
bool SpectrumList_Thermo::hasIonMobility() const {return false;}
bool SpectrumList_Thermo::canConvertIonMobilityAndCCS() const {return false;}
bool SpectrumList_Thermo::hasCombinedIonMobility() const {return false;}
double SpectrumList_Thermo::ionMobilityToCCS(double ionMobility, double mz, int charge) const {return 0;}
double SpectrumList_Thermo::ccsToIonMobility(double ccs, double mz, int charge) const {return 0;}
SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, DetailLevel detailLevel) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Thermo::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_THERMO
