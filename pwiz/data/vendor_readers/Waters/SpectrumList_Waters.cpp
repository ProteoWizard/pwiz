//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "SpectrumList_Waters.hpp"


#ifdef PWIZ_READER_WATERS
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Reader_Waters_Detail.hpp"
#include <boost/spirit/include/karma.hpp>
#include "boost/foreach_field.hpp"


namespace pwiz {
namespace msdata {
namespace detail {

using namespace Waters;

SpectrumList_Waters::SpectrumList_Waters(MSData& msd, RawDataPtr rawdata, const Reader::Config& config)
    : msd_(msd), rawdata_(rawdata), config_(config), lockmassFunction_(LOCKMASS_FUNCTION_UNINIT)
{
    useDDAProcessor_ = config_.ddaProcessing;
    rawdata_->EnableProcessing(useDDAProcessor_);

    if (useDDAProcessor_)
    {       
        createDDAIndex();
    }
    else
    {
        createIndex();
    }
}


PWIZ_API_DECL size_t SpectrumList_Waters::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Waters::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error("[SpectrumList_Waters::spectrumIdentity()] Bad index: "
                            + lexical_cast<string>(index));
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Waters::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return checkNativeIdFindResult(size_, id);
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, 0.0, 0.0, 0.0, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, detailLevel, 0.0, 0.0, 0.0, pwiz::util::IntegerSet());
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, 0.0, 0.0, 0.0, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, detailLevel, 0.0, 0.0, 0.0, msLevelsToCentroid);
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata, lockmassMzPosScans, lockmassMzNegScans, lockmassTolerance, msLevelsToCentroid);
}

PWIZ_API_DECL bool SpectrumList_Waters::isLockMassFunction(int function) const
{
    if (lockmassFunction_ == LOCKMASS_FUNCTION_UNINIT) // See if we can figure out which is the lockmass function
    {
        bool hasChromMS = false;
        const set<int>& functionsWithChromFiles = rawdata_->FunctionsWithChromFiles();
        int apparentLockmassFunction = LOCKMASS_FUNCTION_UNINIT;
        BOOST_FOREACH(int tryFunction, rawdata_->FunctionIndexList())
        {
            int msLevel;
            CVID spectrumType;
            translateFunctionType(WatersToPwizFunctionType(rawdata_->Info.GetFunctionType(tryFunction)), msLevel, spectrumType);
            if (cv::cvIsA(spectrumType, MS_mass_spectrum))
            {
                // Function has MS data - but does it have a _CHRO*.dat file? We've observed that lockmass functions don't
                if (functionsWithChromFiles.find(tryFunction) == functionsWithChromFiles.end())
                {
                    apparentLockmassFunction = tryFunction; // No _CHRO*.DAT file, might be lockmass (value is 0-based)
                }
                else
                {
                    hasChromMS = true; // At least one function does have a _CHRO*.dat file
                }
            }
        }
        lockmassFunction_ = hasChromMS ? apparentLockmassFunction : LOCKMASS_FUNCTION_UNKNOWN;
    }
    return function == lockmassFunction_;
}

PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    if (index >= size_)
        throw runtime_error("[SpectrumList_Waters::spectrum()] Bad index: "
                            + lexical_cast<string>(index));

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Waters::spectrum()] Allocation error.");

    IndexEntry& ie = index_[index];

    result->index = ie.index;
    result->id = ie.id;
    int scanStatIndex = ie.block >= 0 ? ie.block : ie.scan; // ion mobility scans get stats based on block index

    // TODO: fix Serializer_mzXML so it supports non-default sourceFilePtrs
    //size_t sourceFileIndex = ie.functionPtr->getFunctionNumber() - 1;
    //result->sourceFilePtr = msd_.fileDescription.sourceFilePtrs[sourceFileIndex];

    /*float laserAimX = pExScanStats_->LaserAimXPos;
    float laserAimY = pExScanStats_->LaserAimYPos;
    //if (scanInfo->ionizationType() == IonizationType_MALDI)
    {
        result->spotID = (format("%dx%d") % laserAimX % laserAimY).str();
    }*/

    result->scanList.set(MS_no_combination);
    result->scanList.scans.push_back(Scan());
    Scan& scan = result->scanList.scans[0];
    scan.instrumentConfigurationPtr = msd_.run.defaultInstrumentConfigurationPtr;

    //scan.instrumentConfigurationPtr =
        //findInstrumentConfiguration(msd_, translate(scanInfo->massAnalyzerType()));

    int msLevel;
    CVID spectrumType;
    translateFunctionType(WatersToPwizFunctionType(rawdata_->Info.GetFunctionType(ie.function)), msLevel, spectrumType);
    
    bool isMS = cv::cvIsA(spectrumType, MS_mass_spectrum);
    CVID xUnit = isMS ? MS_m_z : UO_nanometer;

    double collisionEnergy = 0;
    string collisionEnergyStr = isMS ? rawdata_->GetScanStat(ie.function, scanStatIndex, MassLynxScanItem::COLLISION_ENERGY) : "";
    if (!collisionEnergyStr.empty())
        collisionEnergy = abs(lexical_cast<double>(collisionEnergyStr));

    // heuristic to detect high-energy MSe function (must run for DetailLevel_InstantMetadata so that msLevel is the same at all detail levels)
    if (msLevel == 1 && ie.function == 1 && collisionEnergy > 0 && !isLockMassFunction(ie.function))
    {
        double collisionEnergyFunction1 = 0;
        string collisionEnergyStrFunction1 = rawdata_->GetScanStat(0, scanStatIndex, MassLynxScanItem::COLLISION_ENERGY);
        /* FIXME: this heuristic is broken; something else is needed or user must manually specify whether to assume first two MS1 functions are actually MSe data
        if (!collisionEnergyStrFunction1.empty())
            collisionEnergyFunction1 = lexical_cast<double>(collisionEnergyStrFunction1);
        */
        if (collisionEnergy > collisionEnergyFunction1)
        {
            msLevel = 2;
            spectrumType = MS_MSn_spectrum;
        }
    }

    result->set(spectrumType);
    if (isMS) result->set(MS_ms_level, msLevel);
    scan.set(MS_preset_scan_configuration, ie.function + 1);

    if (detailLevel == DetailLevel_InstantMetadata)
        return result;

    double scanStartTimeInMinutes = rawdata_->Info.GetRetentionTime(ie.function, scanStatIndex);
    scan.set(MS_scan_start_time, scanStartTimeInMinutes, UO_minute);

    if (isMS)
    {
        PwizPolarityType polarityType = WatersToPwizPolarityType(rawdata_->Info.GetIonMode(ie.function));
        if (polarityType != PolarityType_Unknown)
            result->set(translate(polarityType));

        double lockmassMz = (polarityType == PolarityType_Negative) ? lockmassMzNegScans : lockmassMzPosScans;
        if (lockmassMz != 0.0)
        {
            if (detailLevel == DetailLevel_FullData && !rawdata_->ApplyLockMass(lockmassMz, lockmassTolerance))
                warn_once("[SpectrumList_Waters] failed to apply lockmass correction");
        }
        else
            rawdata_->RemoveLockMass();
    }

    bool isProfile = rawdata_->Info.IsContinuum(ie.function);
    if (isProfile)
        result->set(MS_profile_spectrum); // let peakPicker know this was a profile spectrum even if centroiding is requested
    else
        result->set(MS_centroid_spectrum);

    bool doCentroid = msLevelsToCentroid.contains(msLevel) && isProfile;

    if (doCentroid && ie.block >= 0)
    {
        warn_once("[SpectrumList_Waters]: vendor centroiding is not supported for Waters ion mobility data");
        doCentroid = false;
    }

    if (doCentroid)
    {
        result->set(MS_centroid_spectrum);
    }
    else // the following metadata values are only set for profile data
    {
        // block >= 0 is ion mobility
        if (ie.block < 0 || config_.combineIonMobilitySpectra)
        {
            int scan = ie.block < 0 ? ie.scan : ie.block;
            // scanStats values don't match the ion mobility data arrays
            // CONSIDER: in the ion mobility case, get these values from the actual data arrays
            result->set(MS_base_peak_m_z, rawdata_->GetScanStat<double>(ie.function, scan, MassLynxScanItem::BASE_PEAK_MASS));
            result->set(MS_base_peak_intensity, rawdata_->GetScanStat<double>(ie.function, scan, MassLynxScanItem::BASE_PEAK_INTENSITY));
            result->set(MS_total_ion_current, rawdata_->GetScanStat<double>(ie.function, scan, MassLynxScanItem::TOTAL_ION_CURRENT));
            result->defaultArrayLength = rawdata_->GetScanStat<int>(ie.function, scan, MassLynxScanItem::PEAKS_IN_SCAN);
        }
        else
        {
            result->set(MS_total_ion_current, rawdata_->TicByFunctionIndex()[ie.function].at(ie.block));
            result->defaultArrayLength = 0;
        }
    }

    float minMZ = 0, maxMZ = 0;
    if (isMS)
    {
        rawdata_->Info.GetAcquisitionMassRange(ie.function, minMZ, maxMZ);
        scan.scanWindows.push_back(ScanWindow(minMZ, maxMZ, xUnit));

        if (!config_.combineIonMobilitySpectra && hasSonarFunctions())
        {
            float minQuadMz, maxQuadMz;
            rawdata_->Info.GetPrecursorMassRange(ie.function, ie.scan, minQuadMz, maxQuadMz); // Get the quadrupole filter range this spectrum
            scan.userParams.emplace_back("scanning quadrupole position lower bound", toString(minQuadMz), "xsd:float", MS_m_z);
            scan.userParams.emplace_back("scanning quadrupole position upper bound", toString(maxQuadMz), "xsd:float", MS_m_z);
        }
    }

    if (detailLevel < DetailLevel_FastMetadata)
        return result;

    // block >= 0 is ion mobility or SONAR
    if (ie.block >= 0 && !hasSonarFunctions())
    {
        double driftTime = rawdata_->GetDriftTime(ie.function, ie.scan);
        scan.set(MS_ion_mobility_drift_time, driftTime, UO_millisecond);
    }

    if (msLevel > 1 && isMS)
    {
        double setMass = 0;
        if (useDDAProcessor_)
        {
            setMass = ie.setMass;
        }
        else if (!hasSonarFunctions())
        {
            string setMassStr = rawdata_->GetScanStat(ie.function, scanStatIndex, MassLynxScanItem::SET_MASS);
            if (!setMassStr.empty())
                setMass = lexical_cast<double>(setMassStr);
        }

        Precursor precursor;

        float lowerIsolationWindowOffset, maxIsolationWindowOffset;
        bool offsetsPresent = useDDAProcessor_ ? rawdata_->GetIsolationWindow(lowerIsolationWindowOffset, maxIsolationWindowOffset) : false;
        
        if (setMass == 0)
        {
            setMass = (maxMZ + minMZ) / 2;
            precursor.isolationWindow.set(MS_isolation_window_upper_offset, maxMZ - setMass, xUnit);
            precursor.isolationWindow.set(MS_isolation_window_lower_offset, maxMZ - setMass, xUnit);
        }
        else if (offsetsPresent)
        {
            precursor.isolationWindow.set(MS_isolation_window_lower_offset, lowerIsolationWindowOffset, xUnit);
            precursor.isolationWindow.set(MS_isolation_window_upper_offset, maxIsolationWindowOffset, xUnit);
        }
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, setMass, xUnit);

        precursor.activation.set(MS_beam_type_collision_induced_dissociation); // AFAIK there is no Waters instrument with a trap collision cell

        if (collisionEnergy > 0)
            precursor.activation.set(MS_collision_energy, collisionEnergy, UO_electronvolt);


        double precursorMass = useDDAProcessor_ ? ie.precursorMass : setMass;
        SelectedIon selectedIon(precursorMass);

        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);
    }

    if (detailLevel < DetailLevel_FullMetadata)
        return result;

    if (detailLevel == DetailLevel_FullData || detailLevel == DetailLevel_FullMetadata)
    {
        BinaryData<double> mzArray, intensityArray;

        if (ie.block >= 0 && config_.combineIonMobilitySpectra)
        {
            if (detailLevel == DetailLevel_FullMetadata)
                return result;

            auto mobilityOrQuadLowArray = boost::make_shared<BinaryDataArray>();
            auto quadHighArray = boost::make_shared<BinaryDataArray>();
            getCombinedSpectrumData(ie.function, ie.block, mzArray, intensityArray, mobilityOrQuadLowArray->data, quadHighArray->data, doCentroid);
            result->defaultArrayLength = mzArray.size();

            result->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts); // Donate mass and intensity buffers to result vectors

            if (hasSonarFunctions())
            {
                mobilityOrQuadLowArray->set(MS_scanning_quadrupole_position_lower_bound_m_z_array);
                result->binaryDataArrayPtrs.push_back(mobilityOrQuadLowArray);
                quadHighArray->set(MS_scanning_quadrupole_position_upper_bound_m_z_array);
                result->binaryDataArrayPtrs.push_back(quadHighArray);
            }
            else
            {
                 mobilityOrQuadLowArray->set(MS_raw_ion_mobility_array);
                 result->binaryDataArrayPtrs.push_back(mobilityOrQuadLowArray);
            }
        }
        else
        {
            vector<float> masses, intensities;

            if (useDDAProcessor_)
            {
                getDDAScan(index, masses, intensities);
            }
            else if (ie.block >= 0 && !doCentroid && !isLockMassFunction(ie.function)) // Lockmass won't have IMS
            {
                MassLynxRawScanReader& scanReader = rawdata_->GetCompressedDataClusterForBlock(ie.function, ie.block);
                scanReader.ReadScan(ie.function, ie.block, ie.scan, masses, intensities);
                result->defaultArrayLength = masses.size();

                if (detailLevel == DetailLevel_FullMetadata)
                    return result;
            }
            else // not ion mobility
            {
                rawdata_->ReadScan(ie.function, ie.scan, doCentroid, masses, intensities);
                if (doCentroid)
                    calculatePeakMetadata(result, masses, intensities);
                result->defaultArrayLength = masses.size();
            }

            if (detailLevel == DetailLevel_FullData)
            {
                mzArray.resize(masses.size());
                intensityArray.resize(masses.size());
                auto mzArrayItr = mzArray.begin();
                auto intensityArrayItr = intensityArray.begin();
                for (size_t i = 0; i < masses.size(); ++i, ++mzArrayItr, ++intensityArrayItr)
                {
                    *mzArrayItr = masses[i];
                    *intensityArrayItr = intensities[i];
                }
                result->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts); // Donate mass and intensity buffers to result vectors
            }
        }
    }

    return result;
}


PWIZ_API_DECL bool SpectrumList_Waters::hasSonarFunctions() const
{
    return rawdata_->HasSONAR();
}

PWIZ_API_DECL pair<int, int> SpectrumList_Waters::sonarMzToBinRange(double precursorMz, double tolerance) const
{
    pair<int, int> binRange;
    rawdata_->GetSonarRange(precursorMz, tolerance, binRange.first, binRange.second);
    return binRange;
}

PWIZ_API_DECL double SpectrumList_Waters::sonarBinToPrecursorMz(int bin) const
{
    return rawdata_->SonarBinToPrecursorMz(bin);
}

PWIZ_API_DECL bool SpectrumList_Waters::hasIonMobility() const
{
    return rawdata_->HasIonMobility();
}

PWIZ_API_DECL bool SpectrumList_Waters::hasCombinedIonMobility() const
{
    return rawdata_->HasIonMobility() && config_.combineIonMobilitySpectra;
}

PWIZ_API_DECL bool SpectrumList_Waters::canConvertIonMobilityAndCCS() const
{
    return rawdata_->HasCcsCalibration() && !rawdata_->HasSONAR(); // SONAR uses IMS hardware but doesn't involve CCS
}

PWIZ_API_DECL double SpectrumList_Waters::ionMobilityToCCS(double ionMobility, double mz, int charge) const
{
    return rawdata_->DriftTimeToCCS((float) ionMobility, (float) mz, charge);
}

PWIZ_API_DECL double SpectrumList_Waters::ccsToIonMobility(double ccs, double mz, int charge) const
{
    return rawdata_->CcsToDriftTime((float) ccs, (float) mz, charge);
}

PWIZ_API_DECL void SpectrumList_Waters::initializeCoefficients() const
{
    if (calibrationCoefficients_.empty())
    {    
        // the header property is something like: 6.08e-3,9.99e-1,-2.84e-6,7.63e-8,-6.91e-10,T1
        string coefficients = rawdata_->GetHeaderProp("Cal Function 1");
        vector<string> tokens;
        bal::split(tokens, coefficients, bal::is_any_of(","));
        if (tokens.size() < 5) // I don't know what the T1 is for, but it doesn't seem important
            throw runtime_error("[SpectrumList_Waters::spectrum()] error parsing calibration coefficients: " + coefficients);
        calibrationCoefficients_.resize(5);
        try
        {
            for (size_t i=0; i < 5; ++i)
                calibrationCoefficients_[i] = lexical_cast<double>(tokens[i]);
        }
        catch (bad_lexical_cast&)
        {
            throw runtime_error("[SpectrumList_Waters::spectrum()] error parsing calibration coefficients: " + coefficients);
        }
    }
}

PWIZ_API_DECL double SpectrumList_Waters::calibrate(const double& mz) const
{
    const vector<double>& c = calibrationCoefficients_;
    double sqrtMz = sqrt(mz);
    double x = c[0] + c[1]*sqrtMz + c[2]*mz + c[3]*mz*sqrtMz + c[4]*mz*mz;
    return x*x;
}


PWIZ_API_DECL pwiz::analysis::Spectrum3DPtr SpectrumList_Waters::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const
{
    pwiz::analysis::Spectrum3DPtr result(new pwiz::analysis::Spectrum3D);

    if (scanTimeToFunctionAndBlockMap_.empty()) // implies no ion mobility data
        return result;

    boost::container::flat_map<double, vector<pair<int, int> > >::const_iterator findItr = scanTimeToFunctionAndBlockMap_.lower_bound(floor(scanStartTime * 1e6) / 1e6);
    if (findItr == scanTimeToFunctionAndBlockMap_.end() || findItr->first - 1e-6 > scanStartTime)
        return result;

    for (size_t pair = 0; pair < findItr->second.size(); ++pair)
    {
        int function = findItr->second[pair].first;
        int block = findItr->second[pair].first;

        //int axisLength = 0, nonZeroDataPoints = 0;
        int numScansInBlock = 0;

        MassLynxRawScanReader& cdc = rawdata_->GetCompressedDataClusterForBlock(function, block);
        vector<float>& imsMasses = imsMasses_;
        vector<float>& imsIntensities = imsIntensities_;

        numScansInBlock = rawdata_->Info.GetDriftScanCount(function);

        for (int scan = 0; scan < numScansInBlock; ++scan)
        {
            double driftTime = rawdata_->GetDriftTime(function, scan);

            if (driftTimeRanges.find(driftTime) == driftTimeRanges.end())
                continue;

            cdc.ReadScan(function, block, scan, imsMasses, imsIntensities);

            boost::container::flat_map<double, float>& driftSpectrum = (*result)[driftTime];

            //driftSpectrum[imsCalibratedMasses_[imsMasses[0] - 1]] = 0;
            for (int i = 0, end = imsMasses.size(); i < end; ++i)
            {
                driftSpectrum[imsMasses[i]] = imsIntensities[i];
            }
            //driftSpectrum[imsCalibratedMasses_[massIndices[nonZeroDataPoints - 1] + 1]] = 0;
        }
    }

    return result;
}

PWIZ_API_DECL void SpectrumList_Waters::getCombinedSpectrumData(int function, int block, BinaryData<double>& mz, BinaryData<double>& intensity,
    BinaryData<double>& driftTimeOrSonarRangeLow, BinaryData<double>& sonarRangeHigh, bool doCentroid) const
{
    MassLynxRawScanReader& scanReader = rawdata_->GetCompressedDataClusterForBlock(function, block);
    vector<float>& imsMasses = imsMasses_;
    vector<float>& imsIntensities = imsIntensities_;
    const bool wantBins = config_.reportSonarBins && hasSonarFunctions();

    const int numScansInBlock = rawdata_->Info.GetDriftScanCount(function);

    // NB: there's currently no way to know how many points the final array will have; PEAKS_IN_SCAN is a useful heuristic with a bit of expansion factored in
    int totalPoints = rawdata_->GetScanStat<int>(function, block, MassLynxScanItem::PEAKS_IN_SCAN) * 1.5;
    mz.resize(totalPoints);
    intensity.resize(totalPoints);
    driftTimeOrSonarRangeLow.resize(totalPoints);
    const bool wantSonarRanges = !wantBins && hasSonarFunctions();
    if (wantSonarRanges)
    {
        sonarRangeHigh.resize(totalPoints);
    }
    int currentPoints = 0;
    auto mzItr = &mz[0], intensityItr = &intensity[0], driftTimeItr = &driftTimeOrSonarRangeLow[0];
    auto srhItr = wantSonarRanges ? &sonarRangeHigh[0] : nullptr;
    for (int scan = 0; scan < numScansInBlock; ++scan)
    {
        double driftTimeOrQuadLow, quadHigh;
        if (wantBins)
        {
            driftTimeOrQuadLow = scan;
        }
        else if (wantSonarRanges) // using the four array format mz,intensity,quadRangeLow,quadRangeHIgh
        {
            float quadrupoleRangeLow, quadrupoleRangeHigh;
            rawdata_->SonarBinToPrecursorMzRange(scan, quadrupoleRangeLow, quadrupoleRangeHigh);
            driftTimeOrQuadLow = quadrupoleRangeLow;
            quadHigh = quadrupoleRangeHigh;
        }
        else
        {
            driftTimeOrQuadLow = rawdata_->GetDriftTime(function, scan);

            if (!chemistry::MzMobilityWindow::mobilityValueInBounds(config_.isolationMzAndMobilityFilter, driftTimeOrQuadLow))
                continue;
        }

        scanReader.ReadScan(function, block, scan, imsMasses, imsIntensities);

        for (int i = 0, end = imsMasses.size(); i < end; ++i)
        {
            if (config_.ignoreZeroIntensityPoints && imsIntensities[i] == 0)
                continue;
            if (currentPoints >= totalPoints)
            {
                totalPoints = currentPoints * 1.5;
                mz.resize(totalPoints);
                intensity.resize(totalPoints);
                driftTimeOrSonarRangeLow.resize(totalPoints);
                mzItr = &mz[currentPoints];
                intensityItr = &intensity[currentPoints];
                driftTimeItr = &driftTimeOrSonarRangeLow[currentPoints];
                if (wantSonarRanges)
                {
                    sonarRangeHigh.resize(totalPoints);
                    srhItr = &sonarRangeHigh[currentPoints];
                }
            }
            *mzItr++ = imsMasses[i];
            *intensityItr++ = imsIntensities[i];
            *driftTimeItr++ = driftTimeOrQuadLow;
            if (wantSonarRanges)
            {
                *srhItr++ = quadHigh;
            }
            ++currentPoints;
        }
    }
    mz.resize(currentPoints);
    intensity.resize(currentPoints);
    driftTimeOrSonarRangeLow.resize(currentPoints);
    if (wantSonarRanges)
    {
        sonarRangeHigh.resize(currentPoints);
    }
}

PWIZ_API_DECL void SpectrumList_Waters::calculatePeakMetadata(SpectrumPtr& spectrum, const vector<float>& mz, const vector<float>& intensity)
{
    double tic = 0;
    if (!mz.empty())
    {
        double bpmz, bpi = -1;
        for (size_t i = 0, end = mz.size(); i < end; ++i)
        {
            tic += intensity[i];
            if (bpi < intensity[i])
            {
                bpi = intensity[i];
                bpmz = mz[i];
            }
        }

        spectrum->set(MS_base_peak_intensity, bpi, MS_number_of_detector_counts);
        spectrum->set(MS_base_peak_m_z, bpmz, MS_m_z);
        spectrum->set(MS_lowest_observed_m_z, mz.front(), MS_m_z);
        spectrum->set(MS_highest_observed_m_z, mz.back(), MS_m_z);
    }

    spectrum->set(MS_TIC, tic, MS_number_of_detector_counts);
}

PWIZ_API_DECL bool SpectrumList_Waters::calibrationSpectraAreOmitted() const
{
    return config_.ignoreCalibrationScans && lockmassFunction_ >= 0;
}

PWIZ_API_DECL void SpectrumList_Waters::createIndex()
{
    using namespace boost::spirit::karma;

    multimap<float, std::pair<int, int> > functionAndScanByRetentionTime;

    int numScansInBlock = 0;   // number of drift scans per compressed block

    for(int function : rawdata_->FunctionIndexList())
    {
        if (config_.ignoreCalibrationScans && isLockMassFunction(function))
        {
            continue;
        }
        int msLevel;
        CVID spectrumType;

        try { translateFunctionType(WatersToPwizFunctionType(rawdata_->Info.GetFunctionType(function)), msLevel, spectrumType); }
        catch(...) // unable to translate function type
        {
            cerr << "[SpectrumList_Waters::createIndex] Unable to translate function type \"" + rawdata_->Info.GetFunctionTypeString(rawdata_->Info.GetFunctionType(function)) + "\"" << endl;
            continue;
        }

        /*cout << "Scan items for function " << function << ":" << endl;
        vector<MassLynxScanItem> items = rawdata_->Info.GetAvailableScanItems(function);
        vector<std::string> itemNames = rawdata_->Info.GetScanItemString(items);
        for (auto item : itemNames)
        {
            cout << "  " << item << "\n";
        }*/

        if (spectrumType == MS_SRM_spectrum ||
            spectrumType == MS_SIM_spectrum ||
            spectrumType == MS_constant_neutral_loss_spectrum ||
            spectrumType == MS_constant_neutral_gain_spectrum)
            continue;

        int scanCount = rawdata_->Info.GetScansInFunction(function);

        if (rawdata_->IonMobilityByFunctionIndex()[function])
        {
            numScansInBlock = rawdata_->Info.GetDriftScanCount(function);

            scanTimeToFunctionAndBlockMap_.reserve(scanCount);

            for (int i = 0; i < scanCount; ++i)
            {
                scanTimeToFunctionAndBlockMap_[rawdata_->Info.GetRetentionTime(function, i) * 60].push_back(make_pair(function, i));
                functionAndScanByRetentionTime.insert(make_pair(rawdata_->Info.GetRetentionTime(function, i), make_pair(function, i)));
            }
        }
        else
        {
            for (int i = 0; i < scanCount; ++i)
                functionAndScanByRetentionTime.insert(make_pair(rawdata_->Info.GetRetentionTime(function, i), make_pair(function, i)));
        }
    }

    typedef pair<int, int> FunctionScanPair;
    BOOST_FOREACH_FIELD((float rt)(const FunctionScanPair& functionScanPair), functionAndScanByRetentionTime)
    {
        if (rawdata_->IonMobilityByFunctionIndex()[functionScanPair.first])
        {
            if (config_.combineIonMobilitySpectra)
            {
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();
                ie.function = functionScanPair.first;
                ie.process = 0;
                ie.block = functionScanPair.second;
                ie.scan = numScansInBlock / 2; // will get avg drift time
                ie.index = index_.size() - 1;

                std::back_insert_iterator<std::string> sink(ie.id);
                generate(sink, "merged=" << int_ << " function=" << int_ << " block=" << int_,
                         (ie.index + 1), (ie.function + 1), (ie.block + 1));
                idToIndexMap_[ie.id] = ie.index;
            }
            else
            {
                for (int j = 0; j < numScansInBlock; ++j)
                {
                    index_.push_back(IndexEntry());
                    IndexEntry& ie = index_.back();
                    ie.function = functionScanPair.first;
                    ie.process = 0;
                    ie.block = functionScanPair.second;
                    ie.scan = j;
                    ie.index = index_.size() - 1;

                    std::back_insert_iterator<std::string> sink(ie.id);
                    generate(sink,
                        "function=" << int_ << " process=" << int_ << " scan=" << int_,
                        (ie.function + 1), ie.process, ((numScansInBlock*ie.block) + ie.scan + 1));
                    idToIndexMap_[ie.id] = ie.index;
                }
            }
        }
        else
        {
            index_.push_back(IndexEntry());
            IndexEntry& ie = index_.back();
            ie.function = functionScanPair.first;
            ie.process = 0;
            ie.block = -1; // block < 0 is not ion mobility
            ie.scan = functionScanPair.second;
            ie.index = index_.size() - 1;

            std::back_insert_iterator<std::string> sink(ie.id);
            generate(sink,
                     "function=" << int_ << " process=" << int_ << " scan=" << int_,
                     (ie.function + 1), ie.process, (ie.scan + 1));
            idToIndexMap_[ie.id] = ie.index;
        }
        rt = 0; // suppress warning
    }

    size_ = index_.size();
}

PWIZ_API_DECL void SpectrumList_Waters::getDDAScan(unsigned int index, vector<float>& masses, vector<float>& intensities) const
{
    using namespace boost::spirit::karma;
    
    float setMass, precursorMass, retentionTime;
    int function, startScan, endScan;
    bool isMS1;
    rawdata_->GetDDAScan(index, retentionTime, function, startScan, endScan, isMS1, setMass, precursorMass, masses, intensities);
}

PWIZ_API_DECL void SpectrumList_Waters::createDDAIndex()
{
    using namespace boost::spirit::karma;

    size_ = rawdata_->GetDDAScanCount();
    index_.resize(size_);

    for (auto i = 0; i < size_; ++i)
    {
        IndexEntry& ie = index_[i];

        float setMass, precursorMass, retentionTime;
        int function, startScan, endScan;
        vector<float> masses, intensities;
        bool isMS1;
        rawdata_->GetDDAScan(i, retentionTime, function, startScan, endScan, isMS1, setMass, precursorMass, masses, intensities);

        ie.function = function;
        ie.process = 0;
        ie.block = -1; // The SDK DDA processor doesn't yet support ion mobility data
        ie.scan = startScan; // While it might combine multiple scans, use the first for getting the metadata
        ie.index = i;
        ie.setMass = setMass;
        ie.precursorMass = precursorMass;

        std::back_insert_iterator<std::string> sink(ie.id);
        if (startScan == endScan)
        {
            generate(sink,
                    "function=" << int_ << " process=" << int_ << " scan=" << int_,
                    ie.function + 1, ie.process, ie.scan + 1);
        } 
        else
        {
            generate(sink,
                    "merged=" << int_ << " function=" << int_ << " process=" << int_ << " scans=" << int_ << "-" << int_,
                    ie.index, ie.function + 1, ie.process, startScan + 1, endScan + 1);
        }
        idToIndexMap_[ie.id] = ie.index;
    }
}


} // detail
} // msdata
} // pwiz

#else // PWIZ_READER_WATERS

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {
namespace detail {

namespace {const SpectrumIdentity emptyIdentity;}

size_t SpectrumList_Waters::size() const {return 0;}
const SpectrumIdentity& SpectrumList_Waters::spectrumIdentity(size_t index) const {return emptyIdentity;}
size_t SpectrumList_Waters::find(const std::string& id) const {return 0;}
bool SpectrumList_Waters::hasIonMobility() const {return false;}
bool SpectrumList_Waters::hasCombinedIonMobility() const {return false;}
bool SpectrumList_Waters::canConvertIonMobilityAndCCS() const {return false;}
double SpectrumList_Waters::ionMobilityToCCS(double ionMobility, double mz, int charge) const {return 0;}
double SpectrumList_Waters::ccsToIonMobility(double ccs, double mz, int charge) const {return 0;}
bool SpectrumList_Waters::isLockMassFunction(int function) const {return false;}
bool SpectrumList_Waters::calibrationSpectraAreOmitted() const {return false;}
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel) const { return SpectrumPtr(); }
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const { return SpectrumPtr(); }
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel, const pwiz::util::IntegerSet& msLevelsToCentroid) const { return SpectrumPtr(); }
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const { return SpectrumPtr(); }
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, DetailLevel detailLevel, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance, const pwiz::util::IntegerSet& msLevelsToCentroid) const { return SpectrumPtr(); }
pwiz::analysis::Spectrum3DPtr SpectrumList_Waters::spectrum3d(double scanStartTime, const boost::icl::interval_set<double>& driftTimeRanges) const { return pwiz::analysis::Spectrum3DPtr(); }
bool SpectrumList_Waters::hasSonarFunctions() const { return false; }
pair<int, int> SpectrumList_Waters::sonarMzToBinRange(double precursorMz, double tolerance) const { return pair<int, int>(); }
double SpectrumList_Waters::sonarBinToPrecursorMz(int bin) const {return 0;}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_WATERS
