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


#include "SpectrumList_mzXML.hpp"
#include "IO.hpp"
#include "References.hpp"
#include "minimxml/SAXParser.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/iostreams/positioning.hpp"
#include <iostream>
#include <stdexcept>
#include <iterator>


namespace pwiz {
namespace msdata {


using namespace std;
using namespace pwiz::minimxml;
using boost::shared_ptr;
using boost::lexical_cast;
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
            string precursorScanNum, precursorIntensity, precursorCharge;
            getAttribute(attributes, "precursorScanNum", precursorScanNum);
            getAttribute(attributes, "precursorIntensity", precursorIntensity);
            getAttribute(attributes, "precursorCharge", precursorCharge);
            
            precursor->spectrumID = precursorScanNum;

            if (!precursorIntensity.empty())
                precursor->ionSelection.cvParams.push_back(CVParam(MS_intensity, precursorIntensity));

            if (!precursorCharge.empty())
                precursor->ionSelection.cvParams.push_back(CVParam(MS_charge_state, precursorCharge));

            return Status::Ok;
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerPrecursor] Unexpected element name: " + name).c_str());
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (!precursor)
            throw runtime_error("[SpectrumList_mzXML::HandlerPrecursor] Null precursor."); 

        precursor->ionSelection.cvParams.push_back(CVParam(MS_m_z, text));

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
            string precision, byteOrder, pairOrder;
            getAttribute(attributes, "precision", precision);
            getAttribute(attributes, "byteOrder", byteOrder);
            getAttribute(attributes, "pairOrder", pairOrder);

            if (precision == "32")
                config_.precision = BinaryDataEncoder::Precision_32;
            else if (precision == "64")
                config_.precision = BinaryDataEncoder::Precision_64;
            else
                throw runtime_error("[SpectrumList_mzXML::HandlerPeaks] Invalid precision."); 

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
            return Status::Ok;

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

    HandlerScan(Spectrum& spectrum, bool getBinaryData)
    :   spectrum_(spectrum), 
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
                msInstrumentID;

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

            spectrum_.id = num;
            spectrum_.nativeID = num;
            spectrum_.set(MS_ms_level, msLevel);
            spectrum_.sourceFilePosition = position;

            peaksCount_ = lexical_cast<unsigned int>(peaksCount);
            spectrum_.defaultArrayLength = peaksCount_;

            Scan& scan = spectrum_.spectrumDescription.scan;

            scan.cvParams.push_back(CVParam(MS_preset_scan_configuration, scanEvent));

            if (polarity == "+")
                scan.cvParams.push_back(MS_positive_scan);
            else if (polarity == "-")
                scan.cvParams.push_back(MS_negative_scan);

            collisionEnergy_ = collisionEnergy;

            if (!msInstrumentID.empty())
                scan.instrumentPtr = InstrumentPtr(new Instrument(msInstrumentID)); // placeholder 

            if (retentionTime.size()>3 && 
                retentionTime.substr(0,2)=="PT" &&
                retentionTime[retentionTime.size()-1]=='S')
                retentionTime = retentionTime.substr(2,retentionTime.size()-3);
            else
                throw runtime_error("[SpectrumList_mzXML::HandlerScan] Invalid retention time.");

            scan.cvParams.push_back(CVParam(MS_scan_time, retentionTime, MS_second));
            
            spectrum_.spectrumDescription.cvParams.push_back(CVParam(MS_lowest_m_z_value, lowMz));
            spectrum_.spectrumDescription.cvParams.push_back(CVParam(MS_highest_m_z_value, highMz));
            spectrum_.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_m_z, basePeakMz));
            spectrum_.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_intensity, basePeakIntensity));
            spectrum_.spectrumDescription.cvParams.push_back(CVParam(MS_total_ion_current, totIonCurrent));

            return Status::Ok;
        }
        else if (name == "precursorMz")
        {
            spectrum_.spectrumDescription.precursors.push_back(Precursor());
            Precursor& precursor = spectrum_.spectrumDescription.precursors.back();
            precursor.activation.cvParams.push_back(CVParam(MS_collision_energy, collisionEnergy_));
            handlerPrecursor_.precursor = &precursor; 
            return Status(Status::Delegate, &handlerPrecursor_);
        }
        else if (name == "peaks")
        {
            if (!getBinaryData_) return Status::Done;
            handlerPeaks_.peaksCount = peaksCount_;
            return Status(Status::Delegate, &handlerPeaks_);
        }

        throw runtime_error(("[SpectrumList_mzXML::HandlerScan] Unexpected element name: " + name).c_str());
    }

    private:
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

    HandlerScan handler(*result, getBinaryData);
    SAXParser::parse(*is_, handler);

    // hack to get parent scanNumber if precursorScanNum wasn't set

    if (result->cvParam(MS_ms_level).valueAs<int>() > 1 &&
        !result->spectrumDescription.precursors.empty() &&
        result->spectrumDescription.precursors.front().spectrumID.empty())
    {
        result->spectrumDescription.precursors.front().spectrumID = getPrecursorID(index);
    }

    // we can set instrumentPtr if it wasn't set and there is a single Instrument 

    if (!result->spectrumDescription.scan.instrumentPtr.get() &&
        msd_.instrumentPtrs.size() == 1)
    {
        result->spectrumDescription.scan.instrumentPtr = msd_.instrumentPtrs[0];
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

    is_->seekg(-bufferSize, ios::end);
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] Error seeking to end.");

    is_->read(&buffer[0], bufferSize);
    if (!*is_)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] istream not ios::binary?");

    string::size_type indexIndexOffset = buffer.find("<indexOffset>");
    if (indexIndexOffset == string::npos)
        throw index_not_found("[SpectrumList_mzXML::readIndex()] <indexOffset> not found."); 

    is_->seekg(-bufferSize + static_cast<int>(indexIndexOffset), ios::end);
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


SpectrumListPtr SpectrumList_mzXML::create(shared_ptr<istream> is, const MSData& msd, bool indexed)
{
    if (!is.get() || !*is)
        throw runtime_error("[SpectrumList_mzXML::create()] Bad istream.");

    return SpectrumListPtr(new SpectrumList_mzXMLImpl(is, msd, indexed));
}


} // namespace msdata
} // namespace pwiz

