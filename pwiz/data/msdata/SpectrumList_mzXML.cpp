//
// SpectrumList_mzXML.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include "utility/minimxml/SAXParser.hpp"
#include "utility/misc/Exception.hpp"
#include "utility/misc/String.hpp"
#include "utility/misc/Stream.hpp"
#include "utility/misc/Container.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using boost::shared_ptr;
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
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string,size_t> idToIndex_;
    mutable vector<SpectrumPtr> spectrumCache_;

    void readIndex();
    void createIndex();
    void createMaps();
    string getPrecursorID(size_t index) const;
};


SpectrumList_mzXMLImpl::SpectrumList_mzXMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed)
:   is_(is), msd_(msd)
{
    if (indexed)
        readIndex(); 
    else
        createIndex();

    createMaps();
    spectrumCache_.resize(index_.size());
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


size_t SpectrumList_mzXMLImpl::findNative(const string& nativeID) const
{
    return find(nativeID); 
}


void addEmptyMzIntensityArrays(Spectrum& spectrum)
{
    BinaryDataArrayPtr bd_mz(new BinaryDataArray);
    BinaryDataArrayPtr bd_intensity(new BinaryDataArray);

    spectrum.binaryDataArrayPtrs.push_back(bd_mz);
    spectrum.binaryDataArrayPtrs.push_back(bd_intensity);

    bd_mz->cvParams.push_back(MS_m_z_array);
    bd_intensity->cvParams.push_back(MS_intensity_array);
}


struct HandlerPrecursor : public SAXParser::Handler
{
    Precursor* precursor;

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
            string precursorScanNum("0"), precursorIntensity, precursorCharge;
            getAttribute(attributes, "precursorScanNum", precursorScanNum);
            getAttribute(attributes, "precursorIntensity", precursorIntensity);
            getAttribute(attributes, "precursorCharge", precursorCharge);
            
            precursor->spectrumID = precursorScanNum;

            precursor->selectedIons.push_back(SelectedIon());

            if (!precursorIntensity.empty())
                precursor->selectedIons.back().cvParams.push_back(CVParam(MS_intensity, precursorIntensity));

            if (!precursorCharge.empty())
                precursor->selectedIons.back().cvParams.push_back(CVParam(MS_charge_state, precursorCharge));

            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerPrecursor] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor."); 

        precursor->selectedIons.back().cvParams.push_back(CVParam(MS_m_z, text));

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
            addEmptyMzIntensityArrays(spectrum_);
            return Status::Ok;
        }

        BinaryDataEncoder encoder(config_);
        vector<double> decoded;
        encoder.decode(text, decoded);

        if (decoded.size()%2 != 0 || decoded.size()/2 != peaksCount) 
            throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid peak count."); 

        spectrum_.setMZIntensityPairs(reinterpret_cast<const MZIntensityPair*>(&decoded[0]),
                                      peaksCount);
        return Status::Ok;
    }

    virtual Status endElement(const string& name,
                              stream_offset position)
    {
        // hack: avoid reading nested <scan> elements
        // TODO: use nested scans to indicate precursor relationships
        if (name == "peaks") return Status::Done;
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
        peaksCount_(0),
        handlerPeaks_(spectrum)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "scan")
        {
            string num, scanEvent, msLevel, peaksCount, polarity, collisionEnergy, 
                retentionTime, lowMz, highMz, basePeakMz, basePeakIntensity, totIonCurrent,
                msInstrumentID, centroided, deisotoped, chargeDeconvoluted, scanType,
                ionisationEnergy, cidGasPressure, startMz, endMz;

            getAttribute(attributes, "num", num);
            getAttribute(attributes, "scanEvent", scanEvent);
            getAttribute(attributes, "msLevel", msLevel);
            getAttribute(attributes, "peaksCount", peaksCount);
            getAttribute(attributes, "polarity", polarity);
            getAttribute(attributes, "collisionEnergy", collisionEnergy);
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

            spectrum_.id = num;
            spectrum_.nativeID = num;
            spectrum_.sourceFilePosition = position;

            if (msLevel.empty())
                spectrum_.set(MS_ms_level, 1);
            else
                spectrum_.set(MS_ms_level, msLevel);

            peaksCount_ = lexical_cast<unsigned int>(peaksCount);
            spectrum_.defaultArrayLength = peaksCount_;

            Scan& scan = spectrum_.spectrumDescription.scan;

            scan.set(MS_preset_scan_configuration, scanEvent);

            if (polarity == "+")
                scan.set(MS_positive_scan);
            else if (polarity == "-")
                scan.set(MS_negative_scan);

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
            } else if (scanType == "srm")
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

            // assume centroid if not specified
            if (!spectrum_.spectrumDescription.hasCVParam(MS_centroid_mass_spectrum) &&
                centroided == "1")
                spectrum_.spectrumDescription.set(MS_centroid_mass_spectrum);
            else
                spectrum_.spectrumDescription.set(MS_profile_mass_spectrum);

            collisionEnergy_ = collisionEnergy;

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
                    throw runtime_error("[SpectrumList_mzXML::HandlerScan] Invalid retention time.");
                scan.set(MS_scan_time, retentionTime, UO_second);
            }

            if (!startMz.empty() && !endMz.empty())
                scan.scanWindows.push_back(
                    ScanWindow(lexical_cast<double>(startMz), lexical_cast<double>(endMz)));
            
            if (!lowMz.empty())
                spectrum_.spectrumDescription.set(MS_lowest_m_z_value, lowMz);
            if (!highMz.empty())
                spectrum_.spectrumDescription.set(MS_highest_m_z_value, highMz);
            if (!basePeakMz.empty())
                spectrum_.spectrumDescription.set(MS_base_peak_m_z, basePeakMz);
            if (!basePeakIntensity.empty())
                spectrum_.spectrumDescription.set(MS_base_peak_intensity, basePeakIntensity);
            if (!totIonCurrent.empty()) 
                spectrum_.spectrumDescription.set(MS_total_ion_current, totIonCurrent);

            return Status::Ok;
        }
        else if (name == "precursorMz")
        {
            spectrum_.spectrumDescription.precursors.push_back(Precursor());
            Precursor& precursor = spectrum_.spectrumDescription.precursors.back();
            if (!collisionEnergy_.empty())
                precursor.activation.set(MS_collision_energy, collisionEnergy_);
            handlerPrecursor_.precursor = &precursor; 
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "peaks")
        {
            if (!getBinaryData_ || peaksCount_ == 0)
            {
                addEmptyMzIntensityArrays(spectrum_);
                return Status::Done;
            }

            handlerPeaks_.peaksCount = peaksCount_;
            return Status(Status::Delegate, &handlerPeaks_);
        }
        else if (name == "scanOrigin")
        {
            AcquisitionList& al = spectrum_.spectrumDescription.acquisitionList;
            Acquisition a;
            string num, parentFileID;
            getAttribute(attributes, "num", num);
            getAttribute(attributes, "parentFileID", parentFileID);
            a.number = lexical_cast<int>(num);
            if (parentFileID.empty()) // local spectrumRef
            {
                a.spectrumID = num;
            }
            else
            {
                a.sourceFilePtr = SourceFilePtr(new SourceFile(parentFileID));
                a.externalNativeID = num;
            }
            al.acquisitions.push_back(a);
            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerScan] Unexpected element name: " + name).c_str());
    }

    private:
    const MSData& msd_;
    Spectrum& spectrum_;
    bool getBinaryData_;
    string collisionEnergy_;
    unsigned int peaksCount_;
    HandlerPeaks handlerPeaks_;
    HandlerPrecursor handlerPrecursor_;
};


SpectrumPtr SpectrumList_mzXMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Index out of bounds.");

    // returned cached Spectrum if possible

    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Out of memory.");

    result->index = index;

    is_->seekg(offset_to_position(index_[index].sourceFilePosition));
    if (!*is_)
        throw runtime_error("[SpectrumList_mzXML::spectrum()] Error seeking to <scan>.");

    // if file-level dataProcessing says the file is centroid, ignore the centroided attribute
    if (msd_.fileDescription.fileContent.hasCVParam(MS_centroid_mass_spectrum))
        result->spectrumDescription.set(MS_centroid_mass_spectrum);

    HandlerScan handler(msd_, *result, getBinaryData);
    SAXParser::parse(*is_, handler);

    // hack to get parent scanNumber if precursorScanNum wasn't set

    if (result->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        !result->spectrumDescription.precursors.empty() &&
        result->spectrumDescription.precursors.front().spectrumID.empty())
    {
        // MCC: I see your hack and I raise you a hack!
        // * precursorScanNum is optional
        // * the precursor scan is not necessarily in the mzXML
        if (result->spectrumDescription.precursors.front().spectrumID == "0")
            result->spectrumDescription.precursors.front().spectrumID.clear();
        else
            result->spectrumDescription.precursors.front().spectrumID = getPrecursorID(index);
    }

    // we can set instrumentPtr if it wasn't set and there is a single Instrument 

    if (!result->spectrumDescription.scan.instrumentConfigurationPtr.get() &&
        msd_.instrumentConfigurationPtrs.size() == 1)
    {
        result->spectrumDescription.scan.instrumentConfigurationPtr = msd_.instrumentConfigurationPtrs[0];
    }

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

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

    HandlerOffset() : spectrumIdentity(0) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!spectrumIdentity)
            throw runtime_error("[SpectrumList_mzXML::HandlerOffset] Null spectrumIdentity."); 

        if (name != "offset")
            throw runtime_error(("[SpectrumList_mzXML::HandlerOffset] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "id", spectrumIdentity->id);
        spectrumIdentity->nativeID = spectrumIdentity->id; 

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

    HandlerIndex(vector<SpectrumIdentity>& index)
    :   index_(index)
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
            throw runtime_error(("[SpectrumList_mzXML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    private:
    vector<SpectrumIdentity>& index_;
    HandlerOffset handlerOffset_;
};


void SpectrumList_mzXMLImpl::readIndex()
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
        throw index_not_found("[SpectrumList_mzXML::readIndex()] <indexOffset> not found."); 

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

    HandlerIndex handlerIndex(index_);
    SAXParser::parse(*is_, handlerIndex);
    if (index_.empty())
        throw index_not_found("[SpectrumList_mzXML::readIndex()] <index> is empty."); 
}


class HandlerIndexCreator : public SAXParser::Handler
{
    public:

    HandlerIndexCreator(vector<SpectrumIdentity>& index)
    :   index_(index)
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
            si.id = si.nativeID = scanNumber;
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
};


void SpectrumList_mzXMLImpl::createIndex()
{
    is_->seekg(0);
    HandlerIndexCreator handler(index_);
    SAXParser::parse(*is_, handler);
}


void SpectrumList_mzXMLImpl::createMaps()
{
    vector<SpectrumIdentity>::const_iterator it=index_.begin();
    for (unsigned int i=0; i!=index_.size(); ++i, ++it)
        idToIndex_[it->id] = i;
}


string SpectrumList_mzXMLImpl::getPrecursorID(size_t index) const
{
    while (index > 0)
    {
        SpectrumPtr s = spectrum(index-1, false);
        if (s->cvParam(MS_ms_level).valueAs<int>() == 1) return s->id;
        index--;
    }

    throw runtime_error("[SpectrumList_mzXML::getPrecursorScanNumber()] Precursor scan number not found."); 
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

