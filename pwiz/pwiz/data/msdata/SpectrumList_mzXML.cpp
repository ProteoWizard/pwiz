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
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string,size_t> idToIndex_;

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
    CVID nativeIdFormat;

    HandlerPrecursor()
    :   precursor(0)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor."); 

        if (name == "precursorMz")
        {
            string precursorScanNum("0"), precursorIntensity, precursorCharge,
                possibleCharges, activationMethod;
            getAttribute(attributes, "precursorScanNum", precursorScanNum);
            getAttribute(attributes, "precursorIntensity", precursorIntensity);
            getAttribute(attributes, "precursorCharge", precursorCharge);
            getAttribute(attributes, "possibleCharges", possibleCharges);
            getAttribute(attributes, "activationMethod", activationMethod);

            precursor->spectrumID = id::translateScanNumberToNativeID(nativeIdFormat, precursorScanNum);

            precursor->selectedIons.push_back(SelectedIon());

            if (!precursorIntensity.empty() && precursorIntensity != "0")
                precursor->selectedIons.back().cvParams.push_back(CVParam(MS_peak_intensity, precursorIntensity, MS_number_of_counts));

            if (!precursorCharge.empty())
                precursor->selectedIons.back().cvParams.push_back(CVParam(MS_charge_state, precursorCharge));

			if (!possibleCharges.empty())
			{
				vector<string> strCharges;
				boost::algorithm::split(strCharges, possibleCharges, boost::is_any_of(","));

				BOOST_FOREACH(string& charge, strCharges)
				{
					precursor->selectedIons.back().cvParams.push_back(CVParam(MS_possible_charge_state, lexical_cast<int>(charge)));
				}
			}

            if (activationMethod.empty() || activationMethod == "CID")
            {
                // TODO: is it reasonable to assume CID if activation method is unspecified (i.e. older mzXMLs)?
                precursor->activation.set(MS_CID);
            }
            else if (activationMethod == "ETD")
                precursor->activation.set(MS_ETD);
            else if (activationMethod == "ECD")
                precursor->activation.set(MS_ECD);
            //else
                // TODO: log about invalid attribute value

            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerPrecursor] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor."); 

        precursor->selectedIons.back().cvParams.push_back(CVParam(MS_selected_ion_m_z, text, MS_m_z));

        return Status::Ok;
    }
};


class HandlerPeaks : public SAXParser::Handler
{
    public:

    unsigned int peaksCount;

    HandlerPeaks(Spectrum& spectrum)
    :   peaksCount(0), spectrum_(spectrum)
    {}

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

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (peaksCount == 0)
        {
            spectrum_.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
            return Status::Ok;
        }

        BinaryDataEncoder encoder(config_);
        vector<double> decoded;
        encoder.decode(text, decoded);

        if (decoded.size()%2 != 0 || decoded.size()/2 != peaksCount) 
            throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid peak count."); 

        spectrum_.setMZIntensityPairs(reinterpret_cast<const MZIntensityPair*>(&decoded[0]),
                                      peaksCount, MS_number_of_counts);
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

    private:
    Spectrum& spectrum_;
    BinaryDataEncoder::Config config_;
};


class HandlerScan : public SAXParser::Handler
{
    public:

    HandlerScan(const MSData& msd, Spectrum& spectrum, bool getBinaryData)
    :   msd_(msd),
        spectrum_(spectrum), 
        getBinaryData_(getBinaryData),
        handlerPeaks_(spectrum),
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
            string scanEvent, msLevel, peaksCount, polarity,
                retentionTime, lowMz, highMz, basePeakMz, basePeakIntensity, totIonCurrent,
                msInstrumentID, centroided, deisotoped, chargeDeconvoluted, scanType,
                ionisationEnergy, cidGasPressure, startMz, endMz;

            getAttribute(attributes, "num", scanNumber_);
            getAttribute(attributes, "scanEvent", scanEvent);
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

            if (msLevel.empty())
                msLevel = "1";
            spectrum_.set(MS_ms_level, msLevel);

            handlerPeaks_.peaksCount = lexical_cast<unsigned int>(peaksCount);

            spectrum_.scanList.set(MS_no_combination);
            spectrum_.scanList.scans.push_back(Scan());
            Scan& scan = spectrum_.scanList.scans.back();

            scan.set(MS_preset_scan_configuration, scanEvent);

            if (polarity == "+")
                spectrum_.set(MS_positive_scan);
            else if (polarity == "-")
                spectrum_.set(MS_negative_scan);

            // set spectrum and scan type by scanType attribute (assume MSn/Full if absent)
            boost::to_lower(scanType);
            if (scanType.empty() || scanType == "full")
            {
                spectrum_.set(MS_MSn_spectrum);
                scan.set(MS_full_scan);
            } else if (scanType == "zoom")
            {
                spectrum_.set(MS_MSn_spectrum);
                scan.set(MS_zoom_scan);
            } else if (scanType == "sim")
            {
                spectrum_.set(MS_SIM_spectrum);
                scan.set(MS_SIM);
            } else if (scanType == "srm" ||
                // hack: mzWiff (ABI) and wolf-mrm (Waters) use this value
                scanType == "mrm" ||
                // hack: Trapper (Agilent) uses this value
                scanType == "multiplereaction")
            {
                spectrum_.set(MS_SRM_spectrum);
                scan.set(MS_SRM);
            } else if (scanType == "crm")
            {
                spectrum_.set(MS_CRM_spectrum);
                scan.set(MS_CRM);
            } else if (scanType == "q1")
            {
                spectrum_.set(MS_precursor_ion_spectrum);
                scan.set(MS_precursor_ion_scan);
            } else if (scanType == "q3")
            {
                spectrum_.set(MS_product_ion_spectrum);
                scan.set(MS_product_ion_scan);
            }

            // ignore centroided attribute if it was set by the dataProcessing element
            if (!spectrum_.hasCVParam(MS_centroid_spectrum))
            {
                // assume profile if not specified
                if (centroided == "1")
                    spectrum_.set(MS_centroid_spectrum);
                else
                    spectrum_.set(MS_profile_spectrum);
            }

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

            if (!startMz.empty() && !endMz.empty())
                scan.scanWindows.push_back(
                    ScanWindow(lexical_cast<double>(startMz), lexical_cast<double>(endMz), MS_m_z));
            
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
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "peaks")
        {
            if (!getBinaryData_ || handlerPeaks_.peaksCount == 0)
            {
                spectrum_.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_counts);
                spectrum_.defaultArrayLength = handlerPeaks_.peaksCount;
                return Status::Ok;
            }

            return Status(Status::Delegate, &handlerPeaks_);
        }
        else if (name == "nameValue")
        {
            // arbitrary name value pairs are converted to UserParams
            string name, value;
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "value", value);
            spectrum_.userParams.push_back(UserParam(name, value, "xsd:string"));
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

    private:
    const MSData& msd_;
    Spectrum& spectrum_;
    bool getBinaryData_;
    string scanNumber_;
    string collisionEnergy_;
    HandlerPeaks handlerPeaks_;
    HandlerPrecursor handlerPrecursor_;
    CVID nativeIdFormat_;
};


SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Index out of bounds.");

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Out of memory.");

    result->index = index;

    is_->seekg(offset_to_position(index_[index].sourceFilePosition));
    if (!*is_)
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Error seeking to <scan>.");

    // if file-level dataProcessing says the file is centroid, ignore the centroided attribute
    if (msd_.fileDescription.fileContent.hasCVParam(MS_centroid_spectrum))
        result->set(MS_centroid_spectrum);

    HandlerScan handler(msd_, *result, getBinaryData);
    SAXParser::parse(*is_, handler);

    int msLevel = result->cvParam(MS_ms_level).valueAs<int>();
    scanMsLevelCache_[index] = msLevel;

    // hack to get parent scanNumber if precursorScanNum wasn't set

    if (msLevel > 1 &&
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

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

    return result;
}


class HandlerIndexOffset : public SAXParser::Handler
{
    public:

    HandlerIndexOffset(stream_offset& indexOffset)
    :   indexOffset_(indexOffset)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "indexOffset")
            throw runtime_error(("[SpectrumList_mzXML::HandlerIndexOffset] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }

    virtual Status characters(const string& text,
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
    SpectrumIdentity* spectrumIdentity;
    CVID nativeIdFormat;

    HandlerOffset(const MSData& msd)
        :   spectrumIdentity(0),
            nativeIdFormat(id::getDefaultNativeIDFormat(msd)) {}

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

    virtual Status characters(const string& text,
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

    HandlerIndex(vector<SpectrumIdentity>& index, const MSData& msd)
    :   index_(index), handlerOffset_(msd)
    {}

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
            index_.push_back(SpectrumIdentity());
            index_.back().index = index_.size()-1;
            handlerOffset_.spectrumIdentity = &index_.back();
            return Status(Status::Delegate, &handlerOffset_);
        }
        else
          throw SpectrumList_mzXML::index_not_found(("[SpectrumList_mzXML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const std::string& text,
                              stream_offset position)
    {
        throw SpectrumList_mzXML::index_not_found("[SpectrumList_mzXML::HandlerIndex] <index> not found.");
    }

    private:
    vector<SpectrumIdentity>& index_;
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

    HandlerIndexCreator(vector<SpectrumIdentity>& index, const MSData& msd)
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

            SpectrumIdentity si;
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
    vector<SpectrumIdentity>& index_;
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
    vector<SpectrumIdentity>::const_iterator it=index_.begin();
    for (unsigned int i=0; i!=index_.size(); ++i, ++it)
        idToIndex_[it->id] = i;
}


string SpectrumList_mzXMLImpl::getPrecursorID(int precursorMsLevel, size_t index) const
{
    // for MSn spectra (n > 1): return first scan with MSn-1

    while (index > 0)
    {
	    --index;
        int& cachedMsLevel = scanMsLevelCache_[index];
        if (cachedMsLevel == 0)
        {
            // populate the missing MS level
            SpectrumPtr s = spectrum(index-1, false);
	        cachedMsLevel = s->cvParam(MS_ms_level).valueAs<int>();
        }
        if (cachedMsLevel == precursorMsLevel)
            return lexical_cast<string>(index);
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

