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

#include "SpectrumList_mzXML.hpp"
#include "IO.hpp"
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;


namespace {

class SpectrumList_mzXMLImpl : public SpectrumList_mzXML
{
    public:

    SpectrumList_mzXMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed);

    // SpectrumList implementation
    virtual size_t size() const {return index_.size();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(const SpectrumPtr &seed, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, const SpectrumPtr *defaults) const;

    private:
    SpectrumPtr spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, DetailLevel detailLevel, const SpectrumPtr *defaults, bool isRecursiveCall) const;
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentityFromMzXML> index_;
    map<string,size_t> idToIndex_;
    mutable boost::recursive_mutex readMutex;

    mutable vector<int> scanMsLevelCache_;

    bool readIndex(); // return false if index is not present
    void createIndex();
    void createMaps();
    string getPrecursorID(int precursorMsLevel, size_t index) const;
};


SpectrumList_mzXMLImpl::SpectrumList_mzXMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed)
:   is_(is), msd_(msd)
{
    bool gotIndex = false;
    try
    {
      if (indexed)
        gotIndex = readIndex(); 
    } catch (index_not_found e){
      is_->clear();
    }

    if (!gotIndex)
        createIndex();

    scanMsLevelCache_.resize(index_.size());

    createMaps();
}


const SpectrumIdentity& SpectrumList_mzXMLImpl::spectrumIdentity(size_t index) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzXML::spectrumIdentity()] Index out of bounds.");

    return index_[index];
}


size_t SpectrumList_mzXMLImpl::find(const string& id) const
{
    map<string,size_t>::const_iterator it=idToIndex_.find(id);
    return it!=idToIndex_.end() ? it->second : size();
}


struct HandlerPrecursor : public SAXParser::Handler
{
    Precursor* precursor;
    Scan* scan;
    CVID nativeIdFormat;

    HandlerPrecursor()
    :   precursor(0)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor.");

        if (!scan)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null scan.");

        if (name == "precursorMz")
        {
            string precursorScanNum, precursorIntensity, precursorCharge, possibleCharges, activationMethod, windowWideness, driftTime, collisionalCrossSection;
            getAttribute(attributes, "precursorScanNum", precursorScanNum);
            getAttribute(attributes, "precursorIntensity", precursorIntensity);
            getAttribute(attributes, "precursorCharge", precursorCharge);
            getAttribute(attributes, "possibleCharges", possibleCharges);
            getAttribute(attributes, "activationMethod", activationMethod);
            getAttribute(attributes, "windowWideness", windowWideness);
            getAttribute(attributes, "DT", driftTime);
            getAttribute(attributes, "CCS", collisionalCrossSection);

            if (!precursorScanNum.empty()) // precursorScanNum is an optional element
                precursor->spectrumID = id::translateScanNumberToNativeID(nativeIdFormat, precursorScanNum);

            precursor->selectedIons.push_back(SelectedIon());

            if (!precursorIntensity.empty() && precursorIntensity != "0")
                precursor->selectedIons.back().set(MS_peak_intensity, precursorIntensity, MS_number_of_detector_counts);

            if (!precursorCharge.empty())
                precursor->selectedIons.back().set(MS_charge_state, precursorCharge);

            if (!possibleCharges.empty())
            {
                vector<string> strCharges;
                boost::algorithm::split(strCharges, possibleCharges, boost::is_any_of(","));

                BOOST_FOREACH(const string& charge, strCharges)
                {
                    precursor->selectedIons.back().set(MS_possible_charge_state, lexical_cast<int>(charge));
                }
            }

            if (activationMethod.empty() || activationMethod == "CID")
            {
                // TODO: is it reasonable to assume CID if activation method is unspecified (i.e. older mzXMLs)?
                precursor->activation.set(MS_CID);
            }
            else if (activationMethod == "ETD")
                precursor->activation.set(MS_ETD);
            else if (activationMethod == "ETD+SA")
            {
                precursor->activation.set(MS_ETD);
                precursor->activation.set(MS_CID);
            }
            else if (activationMethod == "ECD")
                precursor->activation.set(MS_ECD);
            else if (activationMethod == "HCD")
                precursor->activation.set(MS_HCD);
            //else
                // TODO: log about invalid attribute value

            if (!windowWideness.empty())
            {
                double isolationWindowWidth = lexical_cast<double>(windowWideness) / 2.0;
                precursor->isolationWindow.set(MS_isolation_window_lower_offset, isolationWindowWidth);
                precursor->isolationWindow.set(MS_isolation_window_upper_offset, isolationWindowWidth);
            }

            if (!driftTime.empty())
                scan->set(MS_ion_mobility_drift_time, driftTime, UO_millisecond);

            if (!collisionalCrossSection.empty())
                scan->userParams.emplace_back("CCS", collisionalCrossSection);

            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerPrecursor] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor."); 

        precursor->selectedIons.back().set(MS_selected_ion_m_z, text, MS_m_z);

        return Status::Ok;
    }
};


class HandlerPeaks : public SAXParser::Handler
{
    public:

    unsigned int peaksCount;

    HandlerPeaks(Spectrum& spectrum,unsigned int peakscount)
    :   peaksCount(peakscount), spectrum_(spectrum)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "peaks")
        {
            string precision, byteOrder, pairOrder, compressionType, compressedLen;
            getAttribute(attributes, "precision", precision);
            getAttribute(attributes, "byteOrder", byteOrder);
            getAttribute(attributes, "pairOrder", pairOrder);
            getAttribute(attributes, "compressionType", compressionType);
            getAttribute(attributes, "compressedLen", compressedLen);

            if (precision == "32")
                config_.precision = BinaryDataEncoder::Precision_32;
            else if (precision == "64")
                config_.precision = BinaryDataEncoder::Precision_64;
            else
                throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid precision."); 

            if (!compressionType.empty())
            {
                if (compressionType == "zlib")
                    config_.compression = BinaryDataEncoder::Compression_Zlib;
                else if (compressionType == "none")
                    config_.compression = BinaryDataEncoder::Compression_None;
                else
                    throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid compression type.");
            }

            if (byteOrder=="network" || byteOrder.empty()) // may be empty for older mzXML
                config_.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
            else
                throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid byte order."); 

            if (!pairOrder.empty() && pairOrder!="m/z-int") // may be empty for older mzXML
                throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid pair order."); 
            
            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerPeaks] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        if (peaksCount == 0)
        {
            spectrum_.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
            return Status::Ok;
        }

        BinaryDataEncoder encoder(config_);
        pwiz::util::BinaryData<double> decoded;
        encoder.decode(text.c_str(), text.length(), decoded);

        if (decoded.size()%2 != 0 || decoded.size()/2 != peaksCount) 
            throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid peak count."); 

        spectrum_.setMZIntensityPairs(reinterpret_cast<const MZIntensityPair*>(&decoded[0]),
                                      peaksCount, MS_number_of_detector_counts);
        return Status::Ok;
    }
 
    virtual Status endElement(const std::string& name, stream_offset position)
    {
        if (name == "peaks")
        {
            // hack: this is necessary for handling wolf-mrm generated mzXML,
            // hack: which has no </scan> end tags
            // TODO(dkessner): add unit test to make sure this never breaks

            // hack: avoid reading nested <scan> elements
            // TODO: use nested scans to indicate precursor relationships

            return Status::Done;
        }

        return Status::Ok;
    }

    unsigned int getPeaksCount() const {
        return peaksCount;
    }

    private:
    Spectrum& spectrum_;
    BinaryDataEncoder::Config config_;
};


class HandlerScan : public SAXParser::Handler
{
    public:

    HandlerScan(const MSData& msd, Spectrum& spectrum, const SpectrumIdentityFromMzXML &spectrum_id, bool getBinaryData,size_t peakscount)
    :   msd_(msd),
        spectrum_(spectrum), 
        spectrum_id_(spectrum_id),
        getBinaryData_(getBinaryData),
        handlerPeaks_(spectrum,peakscount),
        handlerPrecursor_(),
        nativeIdFormat_(id::getDefaultNativeIDFormat(msd))
    {
        handlerPrecursor_.nativeIdFormat = nativeIdFormat_;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "scan")
        {
            if (scanNumber_.length())
            {
                // we're in a nested scan declaration, we can quit
                return Status::Done;
            }
            string /*scanEvent,*/ msLevel, polarity,
                retentionTime, lowMz, highMz, basePeakMz, basePeakIntensity, totIonCurrent,
                msInstrumentID, centroided, deisotoped, chargeDeconvoluted, scanType,
                ionisationEnergy, cidGasPressure;

            unsigned int peaksCount;
            double startMz, endMz;

            getAttribute(attributes, "num", scanNumber_);
            //getAttribute(attributes, "scanEvent", scanEvent);
            getAttribute(attributes, "msLevel", msLevel);
            getAttribute(attributes, "peaksCount", peaksCount);
            getAttribute(attributes, "polarity", polarity);
            getAttribute(attributes, "collisionEnergy", collisionEnergy_);
            getAttribute(attributes, "retentionTime", retentionTime);
            getAttribute(attributes, "lowMz", lowMz);
            getAttribute(attributes, "highMz", highMz);
            getAttribute(attributes, "basePeakMz", basePeakMz);
            getAttribute(attributes, "basePeakIntensity", basePeakIntensity);
            getAttribute(attributes, "totIonCurrent", totIonCurrent);
            getAttribute(attributes, "msInstrumentID", msInstrumentID);
            getAttribute(attributes, "centroided", centroided);
            getAttribute(attributes, "deisotoped", deisotoped);
            //TODO: use this: getAttribute(attributes, "chargeDeconvoluted", chargeDeconvoluted);
            getAttribute(attributes, "scanType", scanType);
            //TODO: use this: getAttribute(attributes, "ionisationEnergy", ionisationEnergy);
            //TODO: use this: getAttribute(attributes, "cidGasPressure", cidGasPressure);
            getAttribute(attributes, "startMz", startMz);
            getAttribute(attributes, "endMz", endMz);

            spectrum_.id = id::translateScanNumberToNativeID(nativeIdFormat_, scanNumber_);
            if (spectrum_.id.empty())
                spectrum_.id = "scan=" + lexical_cast<string>(spectrum_.index+1);

            spectrum_.sourceFilePosition = position;

            boost::trim(msLevel);
            if (msLevel.empty())
                msLevel = "1";
            spectrum_.set(MS_ms_level, msLevel);

            handlerPeaks_.peaksCount = peaksCount;

            spectrum_.scanList.set(MS_no_combination);
            spectrum_.scanList.scans.push_back(Scan());
            Scan& scan = spectrum_.scanList.scans.back();

            //scan.set(MS_preset_scan_configuration, scanEvent);

            if (polarity == "+")
                spectrum_.set(MS_positive_scan);
            else if (polarity == "-")
                spectrum_.set(MS_negative_scan);

            // set spectrum and scan type by scanType attribute (assume MSn/Full if absent)
            boost::to_lower(scanType);
            if (scanType.empty() || scanType == "full")
            {
                spectrum_.set(msLevel == "1" ? MS_MS1_spectrum : MS_MSn_spectrum);
            } else if (scanType == "zoom")
            {
                spectrum_.set(MS_MSn_spectrum);
                scan.set(MS_zoom_scan);
            } else if (scanType == "sim")
            {
                spectrum_.set(MS_SIM_spectrum);
                scan.set(MS_SIM);
            } else if (scanType == "srm" ||
                       scanType == "mrm" || // hack: mzWiff (ABI) and wolf-mrm (Waters) use this value
                       scanType == "multiplereaction" || // hack: Trapper (Agilent) uses this value
                       scanType == "srm_ionprep") // hack: (Bruker) uses this value
            {
                spectrum_.set(MS_SRM_spectrum);
                scan.set(MS_SRM);
            } else if (scanType == "crm")
            {
                spectrum_.set(MS_CRM_spectrum);
                scan.set(MS_CRM_OBSOLETE);
            } else if (scanType == "q1")
            {
                spectrum_.set(MS_precursor_ion_spectrum);
                scan.set(MS_precursor_ion_spectrum);
            } else if (scanType == "q3")
            {
                spectrum_.set(MS_product_ion_spectrum);
                scan.set(MS_product_ion_spectrum);
            }

            // TODO: make this more robust
            bool hasCentroidDataProcessing = true;
            if (!msd_.dataProcessingPtrs.empty() && !msd_.dataProcessingPtrs[0]->processingMethods.empty())
                hasCentroidDataProcessing = msd_.dataProcessingPtrs[0]->processingMethods[0].hasCVParam(MS_peak_picking);

            if (centroided.empty())
                spectrum_.set(hasCentroidDataProcessing ? MS_centroid_spectrum : MS_profile_spectrum);
            else if (centroided == "1")
                spectrum_.set(MS_centroid_spectrum);
            else
                spectrum_.set(MS_profile_spectrum);

            if (msInstrumentID.empty() && !msd_.instrumentConfigurationPtrs.empty())
                msInstrumentID = msd_.instrumentConfigurationPtrs[0]->id;
            if (!msInstrumentID.empty())
                scan.instrumentConfigurationPtr = 
                    InstrumentConfigurationPtr(new InstrumentConfiguration(msInstrumentID)); // placeholder 

            if (!retentionTime.empty())
            {
                if (retentionTime.size()>3 &&
                    retentionTime.substr(0,2)=="PT" &&
                    retentionTime[retentionTime.size()-1]=='S')
                    retentionTime = retentionTime.substr(2,retentionTime.size()-3);
                else
                    throw runtime_error("[SpectrumList_mzXML::HandlerScan] Invalid retention time in scan " + scanNumber_);
                scan.set(MS_scan_start_time, retentionTime, UO_second);
            }

            if (endMz <= 0)
            {
                // If instrument settings are omitted, note observed mz range instead
                getAttribute(attributes, "lowMz", startMz);
                getAttribute(attributes, "highMz", endMz);
            }
            if (endMz > 0)
                scan.scanWindows.push_back(ScanWindow(startMz, endMz, MS_m_z));
            
            if (!lowMz.empty())
                spectrum_.set(MS_lowest_observed_m_z, lowMz);
            if (!highMz.empty())
                spectrum_.set(MS_highest_observed_m_z, highMz);
            if (!basePeakMz.empty())
                spectrum_.set(MS_base_peak_m_z, basePeakMz);
            if (!basePeakIntensity.empty())
                spectrum_.set(MS_base_peak_intensity, basePeakIntensity);
            if (!totIonCurrent.empty()) 
                spectrum_.set(MS_total_ion_current, totIonCurrent);

            return Status::Ok;
        }
        else if (name == "precursorMz")
        {
            spectrum_.precursors.push_back(Precursor());
            Precursor& precursor = spectrum_.precursors.back();
            
            if (!collisionEnergy_.empty())
                precursor.activation.set(MS_collision_energy, collisionEnergy_, UO_electronvolt);

            handlerPrecursor_.precursor = &precursor; 
            handlerPrecursor_.scan = spectrum_.scanList.scans.empty() ? NULL : &spectrum_.scanList.scans[0];
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "peaks")
        {
            // pretty likely to come right back here and read the
            // binary data once the header info has been inspected, 
            // so note position
            spectrum_id_.sourceFilePositionForBinarySpectrumData = position; 

            if (!getBinaryData_ || handlerPeaks_.peaksCount == 0)
            {
                spectrum_.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
                spectrum_.defaultArrayLength = handlerPeaks_.peaksCount;
                return Status::Ok;
            }

            return Status(Status::Delegate, &handlerPeaks_);
        }
        else if (name == "nameValue")
        {
            // arbitrary name value pairs are converted to UserParams
            string nameAttr, value;
            getAttribute(attributes, "name", nameAttr);
            getAttribute(attributes, "value", value);
            spectrum_.userParams.push_back(UserParam(nameAttr, value, "xsd:string"));
            return Status::Ok;
        }
        else if (name == "scanOrigin")
        {
            // just ignore
            return Status::Ok;
        }
        else if (name == "nativeScanRef" || name == "coordinate") // mzXML 3.0 beta tags
        {
            // just ignore
            return Status::Ok;
        }
        else if (name == "comment")
        {
            // just ignore
            return Status::Ok;
        }

        throw runtime_error("[SpectrumList_mzXML::HandlerScan] Unexpected element name \"" + name + "\" in scan " + scanNumber_);
    }

    unsigned int getPeaksCount() const {
        return handlerPeaks_.getPeaksCount();
    }

    private:
    const MSData& msd_;
    Spectrum& spectrum_;
    const SpectrumIdentityFromMzXML& spectrum_id_; // for noting binary data position
    bool getBinaryData_;
    string scanNumber_;
    string collisionEnergy_;
    string activationMethod_;
    HandlerPeaks handlerPeaks_;
    HandlerPrecursor handlerPrecursor_;
    CVID nativeIdFormat_;
};

/// retrieve a spectrum by index
/// - detailLevel determines what fields are guaranteed present on the spectrum after the call
/// - client may assume the underlying Spectrum* is valid 
SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index,(detailLevel == DetailLevel_FullData) ? IO::ReadBinaryData : IO::IgnoreBinaryData, detailLevel, NULL, false);
}


SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData,
        getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata,  NULL, false);
}

/// get a copy of the seed spectrum with its binary data populated
/// this is useful for formats like mzXML that can delay loading of binary data
/// - client may assume the underlying Spectrum* is valid 
SpectrumPtr SpectrumList_mzXMLImpl::spectrum(const SpectrumPtr &seed, bool getBinaryData) const {
    return spectrum(seed->index, getBinaryData ? IO::ReadBinaryDataOnly: IO::IgnoreBinaryData, 
        DetailLevel_InstantMetadata, // assume full metadata is already loaded
        &seed, false);
}

SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, const SpectrumPtr *defaults) const
{
    DetailLevel detailLevel;
    switch (binaryDataFlag)
    {
    case IO::IgnoreBinaryData:
        detailLevel = DetailLevel_FullMetadata;
        break;
    case IO::ReadBinaryDataOnly:
        detailLevel = DetailLevel_InstantMetadata;
        break;
    case IO::ReadBinaryData:
    default:
        detailLevel = DetailLevel_FullData;
        break;
    }
    return spectrum(index, binaryDataFlag, detailLevel, defaults, false);
}

SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, DetailLevel detailLevel, const SpectrumPtr *defaults, bool isRecursiveCall) const
{
    boost::lock_guard<boost::recursive_mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Index out of bounds.");

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Out of memory.");
    if (defaults) { // provide some context from previous parser runs
        result = *defaults; // copy in anything we may have cached before
    }

    result->index = index;

	// we may just be here to get binary data of otherwise previously read spectrum
    const SpectrumIdentityFromMzXML &id = index_[index];
	boost::iostreams::stream_offset seekto;
    unsigned int peakscount;
    if (binaryDataFlag==IO::ReadBinaryDataOnly &&
        (id.sourceFilePositionForBinarySpectrumData != (boost::iostreams::stream_offset)-1)) {
        // we're here to add binary data to an already parsed header
        seekto = id.sourceFilePositionForBinarySpectrumData;
        peakscount = id.peaksCount; // expecting this many peaks
    } else {
		seekto = id.sourceFilePosition; // read from start of scan
        peakscount = 0; // don't know how many peaks to expect yet
	}
    is_->seekg(offset_to_position(seekto));
    if (!*is_)
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Error seeking to <scan>.");

    HandlerScan handler(msd_, *result, id, binaryDataFlag!=IO::IgnoreBinaryData, peakscount);
    SAXParser::parse(*is_, handler);

    // note the binary data size in case we come back around to read full data
    if (!id.peaksCount) {
        id.peaksCount = handler.getPeaksCount();
    }

    int msLevel = result->cvParam(MS_ms_level).valueAs<int>();
    scanMsLevelCache_[index] = msLevel;

    if (detailLevel >= DetailLevel_FullMetadata)
    {
        // hack to get parent scanNumber if precursorScanNum wasn't set
        if (msLevel > 1 &&
            !isRecursiveCall && // in an all-MS2 file we can run out of stack
            !result->precursors.empty() &&
            result->precursors.front().spectrumID.empty())
        {
            // MCC: I see your hack and I raise you a hack!
            // * precursorScanNum is optional
            // * the precursor scan is not necessarily in the mzXML
            if (result->precursors.front().spectrumID == "0")
                result->precursors.front().spectrumID.clear();
            else
                result->precursors.front().spectrumID = getPrecursorID(msLevel-1, index);
        }
    }

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

    return result;
}


class HandlerIndexOffset : public SAXParser::Handler
{
    public:

    HandlerIndexOffset(stream_offset& indexOffset)
    :   indexOffset_(indexOffset)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "indexOffset")
            throw runtime_error(("[SpectrumList_mzXML::HandlerIndexOffset] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        indexOffset_ = lexical_cast<stream_offset>(text);
        return Status::Ok;
    }
 
    private:
    stream_offset& indexOffset_;
};


struct HandlerOffset : public SAXParser::Handler
{
    SpectrumIdentityFromMzXML* spectrumIdentity;
    CVID nativeIdFormat;

    HandlerOffset(const MSData& msd)
        :   spectrumIdentity(0),
            nativeIdFormat(id::getDefaultNativeIDFormat(msd))
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumIdentity)
            throw runtime_error("[SpectrumList_mzXML::HandlerOffset] Null spectrumIdentity."); 

        if (name != "offset")
            throw runtime_error(("[SpectrumList_mzXML::HandlerOffset] Unexpected element name: " + name).c_str());

        string scanNumber;
        getAttribute(attributes, "id", scanNumber);
        spectrumIdentity->id = id::translateScanNumberToNativeID(nativeIdFormat, scanNumber);
        if (spectrumIdentity->id.empty())
            spectrumIdentity->id = "scan=" + scanNumber;

        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        if (!spectrumIdentity)
            throw runtime_error("[SpectrumList_mzXML::HandlerOffset] Null spectrumIdentity."); 

        spectrumIdentity->sourceFilePosition = lexical_cast<stream_offset>(text);

        return Status::Ok;
    }
};


class HandlerIndex : public SAXParser::Handler
{
    public:

    HandlerIndex(vector<SpectrumIdentityFromMzXML>& index, const MSData& msd)
    :   index_(index), handlerOffset_(msd)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "index")
        {
            return Status::Ok;
        }
        else if (name == "offset")
        {
            index_.push_back(SpectrumIdentityFromMzXML());
            index_.back().index = index_.size()-1;
            handlerOffset_.spectrumIdentity = &index_.back();
            return Status(Status::Delegate, &handlerOffset_);
        }
        else
          throw SpectrumList_mzXML::index_not_found(("[SpectrumList_mzXML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        throw SpectrumList_mzXML::index_not_found("[SpectrumList_mzXML::HandlerIndex] <index> not found.");
    }

    private:
    vector<SpectrumIdentityFromMzXML>& index_;
    HandlerOffset handlerOffset_;
};


bool SpectrumList_mzXMLImpl::readIndex()
{
    // find <indexOffset>

    const int bufferSize = 512;
    string buffer(bufferSize, '\0');

    is_->seekg(-bufferSize, std::ios::end);
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] Error seeking to end.");

    is_->read(&buffer[0], bufferSize);
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] istream not ios::binary?");

    string::size_type indexIndexOffset = buffer.find("<indexOffset>");
    if (indexIndexOffset == string::npos)
        return false; // no index present 

    is_->seekg(-bufferSize + static_cast<int>(indexIndexOffset), std::ios::end);
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] Error seeking to <indexOffset>."); 
    
    // read <indexOffset>

    stream_offset indexOffset = 0;
    HandlerIndexOffset handlerIndexOffset(indexOffset);
    SAXParser::parse(*is_, handlerIndexOffset);
    if (indexOffset == 0)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] Error parsing <indexOffset>."); 

    // read <index>

    is_->seekg(offset_to_position(indexOffset));
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] Error seeking to <index>."); 

    HandlerIndex handlerIndex(index_, msd_);
    SAXParser::parse(*is_, handlerIndex);
    if (index_.empty())
        throw index_not_found("[SpectrumList_mzXML::readIndex()] <index> is empty.");

    return true;
}


class HandlerIndexCreator : public SAXParser::Handler
{
    public:

    HandlerIndexCreator(vector<SpectrumIdentityFromMzXML>& index, const MSData& msd)
    :   index_(index), nativeIdFormat_(id::getDefaultNativeIDFormat(msd))
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "scan")
        {
            string scanNumber;
            getAttribute(attributes, "num", scanNumber);

            SpectrumIdentityFromMzXML si;
            si.index = index_.size();
            si.id = id::translateScanNumberToNativeID(nativeIdFormat_, scanNumber);
            if (si.id.empty())
                si.id = "scan=" + lexical_cast<string>(si.index+1);
            si.sourceFilePosition = position;

            index_.push_back(si);
        }

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "msRun")
            return Status::Done;

        return Status::Ok;
    }

    private:
    vector<SpectrumIdentityFromMzXML>& index_;
    CVID nativeIdFormat_;
};


void SpectrumList_mzXMLImpl::createIndex()
{
    is_->seekg(0);
    HandlerIndexCreator handler(index_, msd_);
    SAXParser::parse(*is_, handler);
}


void SpectrumList_mzXMLImpl::createMaps()
{
    if (index_.empty())
        return;

    vector<SpectrumIdentityFromMzXML>::const_iterator it=index_.begin();
    for (size_t i = 0; i != index_.size(); ++i, ++it)
        idToIndex_[it->id] = i;

    // for mzXML where nativeID has been interpreted, create secondary mapping just from raw scan numbers
    // NB: translateNativeIDToScanNumber should always work because only nativeIds that can be represented by scan numbers can be interpreted from mzXML
    if (!bal::starts_with(index_.begin()->id, "scan="))
    {
        CVID nativeIdFormat = pwiz::msdata::id::getDefaultNativeIDFormat(msd_);
        it = index_.begin();
        for (size_t i = 0; i != index_.size(); ++i, ++it)
            idToIndex_["scan=" + id::translateNativeIDToScanNumber(nativeIdFormat, it->id)] = i;
    }
}


string SpectrumList_mzXMLImpl::getPrecursorID(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index > 0)
    {
        --index;
        int& cachedMsLevel = scanMsLevelCache_[index];
        if (index && (cachedMsLevel == 0))
        {
            // populate the missing MS level
            SpectrumPtr s = spectrum(index, DetailLevel_FastMetadata); // avoid excessive recursion

            cachedMsLevel = s->cvParam(MS_ms_level).valueAs<int>();
        }
        if (cachedMsLevel == precursorMsLevel) 
        {
            SpectrumPtr s = spectrum(index, DetailLevel_FastMetadata);
            return s ?  s->id : lexical_cast<string>(index);
        }
    }

    return "";
}


} // namespace


PWIZ_API_DECL SpectrumListPtr SpectrumList_mzXML::create(shared_ptr<istream> is, const MSData& msd, bool indexed)
{
    if (!is.get() || !*is)
        throw runtime_error("[SpectrumList_mzXML::create()] Bad istream.");

    return SpectrumListPtr(new SpectrumList_mzXMLImpl(is, msd, indexed));
}


} // namespace msdata
} // namespace pwiz

