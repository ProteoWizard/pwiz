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
#include "boost/shared_ptr.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "Reader_Waters_Detail.hpp"
#include <iostream>
#include <stdexcept>


namespace pwiz {
namespace msdata {
namespace detail {


SpectrumList_Waters::SpectrumList_Waters(MSData& msd, RawDataPtr rawdata)
:   msd_(msd), rawdata_(rawdata)
{
    createIndex();
    size_ = index_.size();
}


PWIZ_API_DECL size_t SpectrumList_Waters::size() const
{
    return size_;
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Waters::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_Waters::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


PWIZ_API_DECL size_t SpectrumList_Waters::find(const string& id) const
{
    map<string, size_t>::const_iterator scanItr = idToIndexMap_.find(id);
    if (scanItr == idToIndexMap_.end())
        return size_;
    return scanItr->second;
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData) const
{
    if (index >= size_)
        throw runtime_error(("[SpectrumList_Waters::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // allocate a new Spectrum
    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_Waters::spectrum()] Allocation error.");

    IndexEntry& ie = index_[index];

    result->index = ie.index;
    result->id = ie.id;

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

    ScanPtr scanPtr = ie.functionPtr->getScan(ie.process, ie.scan);

    int msLevel;
    CVID spectrumType;
    translateFunctionType(ie.functionPtr->getFunctionType(), msLevel, spectrumType);

    result->set(spectrumType);
    result->set(MS_ms_level, msLevel);

    scan.set(MS_preset_scan_configuration, ie.functionPtr->getFunctionNumber());
    scan.set(MS_scan_start_time, scanPtr->getStartTime(), UO_minute);

    //PolarityType polarityType = scanInfo->polarityType();
    //if (polarityType!=PolarityType_Unknown) scan.cvParams.push_back(translate(polarityType));

    if (scanPtr->getDataIsContinuous())
        result->set(MS_profile_spectrum);
    else
        result->set(MS_centroid_spectrum);

    result->set(MS_base_peak_m_z, scanPtr->getBasePeakMZ());
    result->set(MS_base_peak_intensity, scanPtr->getBasePeakIntensity());
    result->set(MS_total_ion_current, scanPtr->getTIC());

    // TODO: get correct values
    scan.scanWindows.push_back(ScanWindow(scanPtr->getMinMZ(), scanPtr->getMaxMZ(), MS_m_z));

    //sd.set(MS_lowest_observed_m_z, minObservedMz);
    //sd.set(MS_highest_observed_m_z, maxObservedMz);

    PrecursorPtr precursorPtr = scanPtr->getPrecursorInfo();

    if (precursorPtr.get())
    {
        Precursor precursor;
        SelectedIon selectedIon(precursorPtr->mz);
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, precursorPtr->mz, MS_m_z);

        precursor.activation.set(MS_CID);
        precursor.activation.set(MS_collision_energy, precursorPtr->collisionEnergy, UO_electronvolt);

        precursor.selectedIons.push_back(selectedIon);
        result->precursors.push_back(precursor);
    }

    result->defaultArrayLength = scanPtr->getNumPoints();

    if (getBinaryData)
    {
	    vector<double> mzArray(scanPtr->masses().begin(), scanPtr->masses().end());
        vector<double> intensityArray(scanPtr->intensities().begin(), scanPtr->intensities().end());
	    result->setMZIntensityArrays(mzArray, intensityArray, MS_number_of_counts);
    }

    return result;
}


PWIZ_API_DECL void SpectrumList_Waters::createIndex()
{
    BOOST_FOREACH(const FunctionPtr& functionPtr, rawdata_->functions())
    {
        int msLevel;
        CVID spectrumType;

        try { translateFunctionType(functionPtr->getFunctionType(), msLevel, spectrumType); }
        catch(...) { continue; } // unable to translate function type

        if (spectrumType == MS_SRM_spectrum ||
            spectrumType == MS_SIM_spectrum ||
            spectrumType == MS_constant_neutral_loss_spectrum ||
            spectrumType == MS_constant_neutral_gain_spectrum)
            continue;

        size_t scanCount = functionPtr->getScanCount();
        for (size_t i=1; i <= scanCount; ++i)
        {
            index_.push_back(IndexEntry());
            IndexEntry& ie = index_.back();
            ie.functionPtr = functionPtr;
            ie.process = 0;
            ie.scan = i;
            ie.index = index_.size()-1;
            ie.id = (format("function=%d process=%d scan=%d")
                        % functionPtr->getFunctionNumber()
                        % ie.process
                        % ie.scan).str();
        }
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
SpectrumPtr SpectrumList_Waters::spectrum(size_t index, bool getBinaryData) const {return SpectrumPtr();}

} // detail
} // msdata
} // pwiz

#endif // PWIZ_READER_WATERS
