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

#include "RAMPAdapter.hpp"
#include "MSDataFile.hpp"
#include "LegacyAdapter.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "boost/static_assert.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::data;


class RAMPAdapter::Impl
{
    public:

    Impl(const string& filename) 
    :   msd_(filename),
        nativeIdFormat_(id::getDefaultNativeIDFormat(msd_)),
        firstIndex_((size_t)-1),
        lastIndex_(0)
    {
        if (!msd_.run.spectrumListPtr.get())
            throw runtime_error("[RAMPAdapter] Null spectrumListPtr.");

        size_ = msd_.run.spectrumListPtr->size();
        firstIndex_ = 0;
        lastIndex_ = size_-1;
    }

    size_t scanCount() const
    {
        return size_;
    }

    size_t index(int scanNumber) const 
    {
        CVID nativeIdFormat = id::getDefaultNativeIDFormat(msd_);
        string scanNumberStr = lexical_cast<string>(scanNumber);
        string id = id::translateScanNumberToNativeID(nativeIdFormat, scanNumberStr);
        if (id.empty()) // unsupported nativeID type
        {
            size_t index = scanNumber-1; // assume scanNumber is a 1-based index
            if (index >= size_)
                throw out_of_range("[RAMPAdapter] scanNumber " + scanNumberStr + " (treated as 1-based index) is out of range");
            return index;
        }
        return msd_.run.spectrumListPtr->find(id);
    }

    int getScanNumber(size_t index) const;
    void getScanHeader(size_t index, ScanHeaderStruct& result, bool reservePeaks) const;
    void getScanPeaks(size_t index, std::vector<double>& result) const;
    void getRunHeader(RunHeaderStruct& result) const;
    void getInstrument(InstrumentStruct& result) const;

    private:
    MSDataFile msd_;
    CVID nativeIdFormat_;
    CVTranslator cvTranslator_;
    vector<bool> nonDefaultSpectra_;
    size_t firstIndex_, lastIndex_;
    size_t size_;
    mutable SpectrumPtr lastSpectrum;
};


namespace {

double retentionTime(const Scan& scan)
{
    CVParam param = scan.cvParam(MS_scan_start_time);
    if (param.units == UO_second) 
        return param.valueAs<double>();
    else if (param.units == UO_minute) 
        return param.valueAs<double>() * 60;
    return 0;
}

} // namespace


int RAMPAdapter::Impl::getScanNumber(size_t index) const
{
    const SpectrumIdentity& si = msd_.run.spectrumListPtr->spectrumIdentity(index);
    string scanNumber = id::translateNativeIDToScanNumber(nativeIdFormat_, si.id);

    if (scanNumber.empty()) // unsupported nativeID type
    {
        // assume scanNumber is a 1-based index, consistent with this->index() method
        return static_cast<int>(index) + 1;
    } 
    else
        return lexical_cast<int>(scanNumber);
}


void RAMPAdapter::Impl::getScanHeader(size_t index, ScanHeaderStruct& result, bool reservePeaks /*= true*/) const
{
    // use previous spectrum if possible
    if (!lastSpectrum.get() || lastSpectrum->index != index)
        lastSpectrum = msd_.run.spectrumListPtr->spectrum(index, reservePeaks);

    SpectrumPtr spectrum = lastSpectrum;

    Scan dummy;
    Scan& scan = spectrum->scanList.scans.empty() ? dummy : spectrum->scanList.scans[0];

    result.seqNum = static_cast<int>(index + 1);
    result.acquisitionNum = getScanNumber(index);
    result.msLevel = spectrum->cvParam(MS_ms_level).valueAs<int>();
    if (result.msLevel>1)
    {
        CVParam dissociationMethod = spectrum->precursors[0].activation.cvParamChild(MS_dissociation_method);
        string dissociationMethodName, fragmentationMethodName;
        if (!(dissociationMethod.empty()))
        {
            dissociationMethodName = dissociationMethod.name();
            fragmentationMethodName = cvTermInfo(dissociationMethod.cvid).shortName();
        }
        int len = min((int)dissociationMethodName.length(), SCANTYPE_LENGTH - 1);
        dissociationMethodName.copy(result.activationMethod, len);
        result.activationMethod[len] = 0; // string.copy does not null terminate
        len = min((int)fragmentationMethodName.length(), SCANTYPE_LENGTH - 1);
        fragmentationMethodName.copy(result.fragmentationMethod, len);
        result.fragmentationMethod[len] = 0; // string.copy does not null terminate
    }
    else
    {
        result.activationMethod[0] = 0;
    }

    result.peaksCount = static_cast<int>(spectrum->defaultArrayLength);
    result.totIonCurrent = spectrum->cvParam(MS_total_ion_current).valueAs<double>();
    result.retentionTime = scan.cvParam(MS_scan_start_time).timeInSeconds();
    result.basePeakMZ = spectrum->cvParam(MS_base_peak_m_z).valueAs<double>();    
    result.basePeakIntensity = spectrum->cvParam(MS_base_peak_intensity).valueAs<double>();    
    result.collisionEnergy = 0;
    result.ionisationEnergy = spectrum->cvParam(MS_ionization_energy_OBSOLETE).valueAs<double>();
    result.lowMZ = spectrum->cvParam(MS_lowest_observed_m_z).valueAs<double>();        
    result.highMZ = spectrum->cvParam(MS_highest_observed_m_z).valueAs<double>();        
    result.precursorScanNum = 0;
    result.precursorMZ = 0;
    result.precursorCharge = 0;
    result.precursorIntensity = 0;
    result.compensationVoltage = 0;
    result.is_centroided = (spectrum->cvParam(MS_centroid_spectrum).name()=="centroid spectrum");
    result.is_negative = (spectrum->cvParam(MS_negative_scan).name()=="negative scan");    

    std::string filterLine = scan.cvParam(MS_filter_string).value;
    
    size_t found = filterLine.find("cv=");
    
    if (found!=string::npos) {
      filterLine = filterLine.substr(found+3);
      found = filterLine.find_first_of(" ");
      if (found!=string::npos) {
          filterLine = filterLine.substr(0, found);
          result.compensationVoltage = atof(filterLine.c_str());
      }
    }

    result.filterLine = filterLine;



    if (!spectrum->precursors.empty())
    {
        const Precursor& precursor = spectrum->precursors[0];
        result.collisionEnergy = precursor.activation.cvParam(MS_collision_energy).valueAs<double>();
        size_t precursorIndex = msd_.run.spectrumListPtr->find(precursor.spectrumID);

        if (precursorIndex < msd_.run.spectrumListPtr->size())
        {
            const SpectrumIdentity& precursorSpectrum = msd_.run.spectrumListPtr->spectrumIdentity(precursorIndex);
            string precursorScanNumber = id::translateNativeIDToScanNumber(nativeIdFormat_, precursorSpectrum.id);
            
            if (precursorScanNumber.empty()) // unsupported nativeID type
            {
                // assume scanNumber is a 1-based index, consistent with this->index() method
                result.precursorScanNum = precursorIndex+1;
            } 
            else 
            {
                result.precursorScanNum = lexical_cast<int>(precursorScanNumber);
            }
        }
        if (!precursor.selectedIons.empty())
        {
            result.precursorMZ = precursor.selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>();
            if (!result.precursorMZ)
            { // mzML 1.0?
                result.precursorMZ = precursor.selectedIons[0].cvParam(MS_m_z).valueAs<double>();
            }
            result.precursorCharge = precursor.selectedIons[0].cvParam(MS_charge_state).valueAs<int>();
            result.precursorIntensity = precursor.selectedIons[0].cvParam(MS_peak_intensity).valueAs<double>();
        }
    }

    BOOST_STATIC_ASSERT(SCANTYPE_LENGTH > 4);
    memset(result.scanType, 0, SCANTYPE_LENGTH);
    strcpy(result.scanType, "Full"); // default
    if (spectrum->hasCVParam(MS_zoom_scan))
        strcpy(result.scanType, "Zoom");

    result.mergedScan = 0; // TODO 
    result.mergedResultScanNum = 0; // TODO 
    result.mergedResultStartScanNum = 0; // TODO 
    result.mergedResultEndScanNum = 0; // TODO 
    result.filePosition = spectrum->sourceFilePosition; 
}


void RAMPAdapter::Impl::getScanPeaks(size_t index, std::vector<double>& result) const
{
    // use previous spectrum if possible (it must have binary data)
    if (!lastSpectrum.get() || lastSpectrum->index != index) {
        lastSpectrum = msd_.run.spectrumListPtr->spectrum(index, true); // full read
    } else if (!lastSpectrum->hasBinaryData()) {
        // copy lastSpectrum header, avoids reread of header if format supports it
        lastSpectrum = msd_.run.spectrumListPtr->spectrum(lastSpectrum, true);
    }

    SpectrumPtr spectrum = lastSpectrum;
    result.clear();
    result.resize(spectrum->defaultArrayLength * 2);
    if (spectrum->defaultArrayLength == 0) return;

    spectrum->getMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(&result[0]), 
                                  spectrum->defaultArrayLength);
}


void RAMPAdapter::Impl::getRunHeader(RunHeaderStruct& result) const
{
    const SpectrumList& spectrumList = *msd_.run.spectrumListPtr;
    result.scanCount = static_cast<int>(size_);

    result.lowMZ = 0; // TODO
    result.highMZ = 0; // TODO
    result.startMZ = 0; // TODO
    result.endMZ = 0; // TODO

    if (size_ == 0) return;

    Scan dummy;

    SpectrumPtr firstSpectrum = spectrumList.spectrum(firstIndex_, false);
    Scan& firstScan = firstSpectrum->scanList.scans.empty() ? dummy : firstSpectrum->scanList.scans[0];
    result.dStartTime = retentionTime(firstScan);

    SpectrumPtr lastSpectrum = spectrumList.spectrum(lastIndex_, false);
    Scan& lastScan = lastSpectrum->scanList.scans.empty() ? dummy : lastSpectrum->scanList.scans[0];
    result.dEndTime = retentionTime(lastScan);
}


namespace {
inline void copyInstrumentString(char* to, const string& from)
{
    strncpy(to, from.substr(0,INSTRUMENT_LENGTH-1).c_str(), INSTRUMENT_LENGTH);
}
} // namespace


void RAMPAdapter::Impl::getInstrument(InstrumentStruct& result) const
{
    const InstrumentConfiguration& instrumentConfiguration = 
        (!msd_.instrumentConfigurationPtrs.empty() && msd_.instrumentConfigurationPtrs[0].get()) ?
        *msd_.instrumentConfigurationPtrs[0] :
        InstrumentConfiguration(); // temporary bound to const reference 

    // this const_cast is ok since we're only calling const functions,
    // but we wish C++ had "const constructors"
    const LegacyAdapter_Instrument adapter(const_cast<InstrumentConfiguration&>(instrumentConfiguration), cvTranslator_); 

    copyInstrumentString(result.manufacturer, adapter.manufacturer());
    copyInstrumentString(result.model, adapter.model());
    copyInstrumentString(result.ionisation, adapter.ionisation());
    copyInstrumentString(result.analyzer, adapter.analyzer());
    copyInstrumentString(result.detector, adapter.detector());
}


//
// RAMPAdapter
//


PWIZ_API_DECL RAMPAdapter::RAMPAdapter(const std::string& filename) : impl_(new Impl(filename)) {}
PWIZ_API_DECL size_t RAMPAdapter::scanCount() const {return impl_->scanCount();}
PWIZ_API_DECL size_t RAMPAdapter::index(int scanNumber) const {return impl_->index(scanNumber);}
PWIZ_API_DECL int RAMPAdapter::getScanNumber(size_t index) const {return impl_->getScanNumber(index);}
PWIZ_API_DECL void RAMPAdapter::getScanHeader(size_t index, ScanHeaderStruct& result, bool reservePeaks) const {impl_->getScanHeader(index, result, reservePeaks);}
PWIZ_API_DECL void RAMPAdapter::getScanPeaks(size_t index, std::vector<double>& result) const {impl_->getScanPeaks(index, result);}
PWIZ_API_DECL void RAMPAdapter::getRunHeader(RunHeaderStruct& result) const {impl_->getRunHeader(result);}
PWIZ_API_DECL void RAMPAdapter::getInstrument(InstrumentStruct& result) const {impl_->getInstrument(result);}


} // namespace msdata
} // namespace pwiz


