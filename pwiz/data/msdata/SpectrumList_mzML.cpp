//
// SpectrumList_mzML.cpp
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


#include "SpectrumList_mzML.hpp"
#include "IO.hpp"
#include "References.hpp"
#include "utility/minimxml/SAXParser.hpp"
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
using boost::iostreams::offset_to_position;


namespace {


class SpectrumList_mzMLImpl : public SpectrumList
{
    public:

    SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed);

    // SpectrumList implementation

    virtual size_t size() const {return index_.size();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual size_t findNative(const std::string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;


    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string,size_t> idToIndex_;
    map<string,size_t> nativeIDToIndex_;
    mutable vector<SpectrumPtr> spectrumCache_;

    void readIndex();
    void createIndex();
    void createMaps();
};


SpectrumList_mzMLImpl::SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed)
:   is_(is), msd_(msd)
{
    if (indexed) 
        readIndex(); 
    else 
        createIndex();

    createMaps();
    spectrumCache_.resize(index_.size());
}


const SpectrumIdentity& SpectrumList_mzMLImpl::spectrumIdentity(size_t index) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzML::spectrumIdentity()] Index out of bounds.");

    return index_[index];
}


size_t SpectrumList_mzMLImpl::find(const string& id) const
{
    map<string,size_t>::const_iterator it=idToIndex_.find(id);
    return it!=idToIndex_.end() ? it->second : size();
}


size_t SpectrumList_mzMLImpl::findNative(const string& nativeID) const
{
    map<string,size_t>::const_iterator it=nativeIDToIndex_.find(nativeID);
    return it!=nativeIDToIndex_.end() ? it->second : size();
}


SpectrumPtr SpectrumList_mzMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Index out of bounds.");

    // returned cached Spectrum if possible

    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Out of memory.");

    is_->seekg(offset_to_position(index_[index].sourceFilePosition));
    if (!*is_) 
        throw runtime_error("[SpectrumList_mzML::spectrum()] Error seeking to <spectrum>.");

    IO::BinaryDataFlag binaryDataFlag = getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData;
    IO::read(*is_, *result, binaryDataFlag);

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


class HandlerIndexListOffset : public SAXParser::Handler
{
    public:

    HandlerIndexListOffset(stream_offset& indexListOffset)
    :   indexListOffset_(indexListOffset)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "indexListOffset")
            throw runtime_error(("[SpectrumList_mzML::HandlerIndexOffset] Unexpected element name: " + name).c_str());
        return Status::Ok;
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        indexListOffset_ = lexical_cast<stream_offset>(text);
        return Status::Ok;
    }
 
    private:
    stream_offset& indexListOffset_;
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
            throw runtime_error("[SpectrumList_mzML::HandlerOffset] Null spectrumIdentity."); 

        if (name != "offset")
            throw runtime_error(("[SpectrumList_mzML::HandlerOffset] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "id", spectrumIdentity->id);
        getAttribute(attributes, "nativeID", spectrumIdentity->nativeID);

        return Status::Ok;
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (!spectrumIdentity)
            throw runtime_error("[SpectrumList_mzML::HandlerOffset] Null spectrumIdentity."); 

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
            throw runtime_error(("[SpectrumList_mzML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    private:
    vector<SpectrumIdentity>& index_;
    HandlerOffset handlerOffset_;
};


class HandlerIndexList : public SAXParser::Handler
{
    public:

    HandlerIndexList(vector<SpectrumIdentity>& index)
    :   handlerIndex_(index)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "indexList")
        {
            return Status::Ok;
        }
        else if (name == "index")
        {
            string indexName;
            getAttribute(attributes, "name", indexName);
            if (indexName == "spectrum")
                return Status(Status::Delegate, &handlerIndex_);
            else
                return Status(Status::Delegate, &dummy_);
        }
        else
            throw runtime_error(("[SpectrumList_mzML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerIndex handlerIndex_;
    SAXParser::Handler dummy_;
};


void SpectrumList_mzMLImpl::readIndex()
{
    // find <indexListOffset>

    const int bufferSize = 512;
    string buffer(bufferSize, '\0');

    is_->seekg(-bufferSize, ios::end);
    is_->read(&buffer[0], bufferSize);

    string::size_type indexIndexOffset = buffer.find("<indexListOffset>");
    if (indexIndexOffset == string::npos)
        throw runtime_error("SpectrumList_mzML::readIndex()] <indexListOffset> not found."); 

    is_->seekg(-bufferSize + static_cast<int>(indexIndexOffset), ios::end);
    if (!*is_)
        throw runtime_error("SpectrumList_mzML::readIndex()] Error seeking to <indexListOffset>."); 
    
    // read <indexListOffset>

    boost::iostreams::stream_offset indexListOffset = 0;
    HandlerIndexListOffset handlerIndexListOffset(indexListOffset);
    SAXParser::parse(*is_, handlerIndexListOffset);
    if (indexListOffset == 0)
        throw runtime_error("SpectrumList_mzML::readIndex()] Error parsing <indexListOffset>."); 

    // read <index>

    is_->seekg(offset_to_position(indexListOffset));
    if (!*is_) 
        throw runtime_error("[SpectrumList_mzML::readIndex()] Error seeking to <index>.");

    HandlerIndexList handlerIndexList(index_);
    SAXParser::parse(*is_, handlerIndexList);
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
        if (name == "spectrum")
        {
            string index, id, nativeID;
            getAttribute(attributes, "index", index);
            getAttribute(attributes, "id", id);
            getAttribute(attributes, "nativeID", nativeID);

            SpectrumIdentity si;
            si.index = lexical_cast<size_t>(index);
            si.id = id;
            si.nativeID = nativeID;
            si.sourceFilePosition = position;

            if (si.index != index_.size())
                throw runtime_error("[SpectrumList_mzML::HandlerIndexCreator] Bad index.");

            index_.push_back(si);
        }

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "spectrumList")
            return Status::Done;

        return Status::Ok;
    }

    private:
    vector<SpectrumIdentity>& index_;
};


void SpectrumList_mzMLImpl::createIndex()
{
    is_->seekg(0);
    HandlerIndexCreator handler(index_);
    SAXParser::parse(*is_, handler);
}


void SpectrumList_mzMLImpl::createMaps()
{
    vector<SpectrumIdentity>::const_iterator it=index_.begin();
    for (size_t i=0; i!=index_.size(); ++i, ++it)
        idToIndex_[it->id] = nativeIDToIndex_[it->nativeID] = i;
}


} // namespace


SpectrumListPtr SpectrumList_mzML::create(shared_ptr<istream> is, const MSData& msd, bool indexed)
{
    if (!is.get() || !*is)
        throw runtime_error("[SpectrumList_mzML::create()] Bad istream.");

    return SpectrumListPtr(new SpectrumList_mzMLImpl(is, msd, indexed));
}


} // namespace msdata
} // namespace pwiz

