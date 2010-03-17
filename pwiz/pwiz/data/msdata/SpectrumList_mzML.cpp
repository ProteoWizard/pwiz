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

#include "SpectrumList_mzML.hpp"
#include "IO.hpp"
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include <boost/thread/once.hpp>
#include <boost/bind.hpp>


namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using boost::shared_ptr;
using boost::iostreams::offset_to_position;


namespace {


// since the call_once implementation on pthreads is defined as an aggregate,
// this proxy class is used for initializing the per-instance once_flags
struct once_flag_proxy
{
    boost::once_flag flag;
};


once_flag_proxy init_once_flag_proxy = {BOOST_ONCE_INIT};


class SpectrumList_mzMLImpl : public SpectrumList_mzML
{
    public:

    SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed);

    // SpectrumList implementation

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual IndexList findSpotID(const std::string& spotID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;


    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    mutable bool indexed_;

    mutable once_flag_proxy indexSizeSet_;
    mutable once_flag_proxy indexInitialized_;
    mutable size_t size_;
    mutable vector<SpectrumIdentity> index_;
    mutable map<string,size_t> idToIndex_;
    mutable map<string,IndexList> spotIDToIndexList_;

    void setIndexSize() const; // set the index size from the spectrumList count attribute
    void readIndex() const;
    void createIndex() const;
    void createMaps() const;
};


SpectrumList_mzMLImpl::SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed)
:   is_(is), msd_(msd),
    indexed_(indexed),
    indexSizeSet_(init_once_flag_proxy),
    indexInitialized_(init_once_flag_proxy)
{
}


size_t SpectrumList_mzMLImpl::size() const
{
    boost::call_once(indexSizeSet_.flag, boost::bind(&SpectrumList_mzMLImpl::setIndexSize, this));
    return size_;
}


const SpectrumIdentity& SpectrumList_mzMLImpl::spectrumIdentity(size_t index) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    if (index > index_.size())
        throw runtime_error("[SpectrumList_mzML::spectrumIdentity()] Index out of bounds.");

    return index_[index];
}


size_t SpectrumList_mzMLImpl::find(const string& id) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    map<string,size_t>::const_iterator it=idToIndex_.find(id);
    return it!=idToIndex_.end() ? it->second : size();
}


IndexList SpectrumList_mzMLImpl::findSpotID(const string& spotID) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    map<string,IndexList>::const_iterator it=spotIDToIndexList_.find(spotID);
    return it!=spotIDToIndexList_.end() ? it->second : IndexList();
}

SpectrumPtr SpectrumList_mzMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    if (index >= index_.size())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Index out of bounds.");

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Out of memory.");

    IO::BinaryDataFlag binaryDataFlag = getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData;
    int schemaVersion = msd_.version().find("1.0") == 0 ? 1 : 0;

    try
    {
        is_->seekg(offset_to_position(index_[index].sourceFilePosition));
        if (!*is_) 
            throw runtime_error("[SpectrumList_mzML::spectrum()] Error seeking to <spectrum>.");

        IO::read(*is_, *result, binaryDataFlag, schemaVersion, &msd_);

        // test for reading the wrong spectrum
        if (result->index != index)
            throw runtime_error("[SpectrumList_mzML::spectrum()] Index entry points to the wrong spectrum.");
    }
    catch (runtime_error&)
    {
        // TODO: log warning about missing/corrupt index

        // recreate index
        indexed_ = false;
        createIndex();

        is_->seekg(offset_to_position(index_[index].sourceFilePosition));
        IO::read(*is_, *result, binaryDataFlag, schemaVersion, &msd_);
    }

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

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

        getAttribute(attributes, "idRef", spectrumIdentity->id);
        getAttribute(attributes, "spotID", spectrumIdentity->spotID);

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
    :   index_(index), offsetCount_(0)
    {
        handlerOffset_.autoUnescapeCharacters = false;
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
            SpectrumIdentity* si;
            if (offsetCount_ < index_.size())
                si = &index_[offsetCount_];
            else // oops - the count attribute was wrong :(
            {
                index_.push_back(SpectrumIdentity());
                si = &index_.back();
            }
            si->index = offsetCount_;
            handlerOffset_.spectrumIdentity = si;
            ++offsetCount_;
            return Status(Status::Delegate, &handlerOffset_);
        }
        else
            throw runtime_error(("[SpectrumList_mzML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    private:
    vector<SpectrumIdentity>& index_;
    size_t offsetCount_;
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


class HandlerSpectrumListCount : public SAXParser::Handler
{
    public:

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "spectrumList")
        {
            getAttribute(attributes, "count", spectrumListCount);
            return Status::Done;
        }
        return Status::Ok;
    }

    size_t getSpectrumListCount() {return spectrumListCount;}

    private:
    size_t spectrumListCount;
};


void SpectrumList_mzMLImpl::setIndexSize() const
{
    is_->seekg(0);
    HandlerSpectrumListCount handlerSpectrumListCount;
    SAXParser::parse(*is_, handlerSpectrumListCount);
    size_ = handlerSpectrumListCount.getSpectrumListCount();
}


void SpectrumList_mzMLImpl::readIndex() const
{
    // find <indexListOffset>

    const int bufferSize = 512;
    string buffer(bufferSize, '\0');

    is_->seekg(-bufferSize, std::ios::end);
    is_->read(&buffer[0], bufferSize);

    string::size_type indexIndexOffset = buffer.find("<indexListOffset>");
    if (indexIndexOffset == string::npos)
        throw runtime_error("SpectrumList_mzML::readIndex()] <indexListOffset> not found."); 

    is_->seekg(-bufferSize + static_cast<int>(indexIndexOffset), std::ios::end);
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
    :   index_(index), spectrumCount_(0)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "spectrum")
        {
            SpectrumIdentity* si;
            if (spectrumCount_ < index_.size())
                si = &index_[spectrumCount_];
            else // oops - the count attribute was wrong :(
            {
                index_.push_back(SpectrumIdentity());
                si = &index_.back();
            }

            getAttribute(attributes, "id", si->id);
            getAttribute(attributes, "spotID", si->spotID);

            si->index = spectrumCount_;
            si->sourceFilePosition = position;

            ++spectrumCount_;
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
    size_t spectrumCount_;
};


void SpectrumList_mzMLImpl::createIndex() const
{
    boost::call_once(indexSizeSet_.flag, boost::bind(&SpectrumList_mzMLImpl::setIndexSize, this));

    index_.clear();

    // resize the index assuming the count attribute is accurate
    index_.resize(size_);

    if (indexed_)
    {
        try
        {
            readIndex();
        }
        catch (runtime_error&)
        {
            // TODO: log warning that the index was corrupt/missing
            is_->clear();
            is_->seekg(0);
            HandlerIndexCreator handler(index_);
            handler.autoUnescapeCharacters = false;
            SAXParser::parse(*is_, handler);
        }
    }
    else
    {
        is_->clear();
        is_->seekg(0);
        HandlerIndexCreator handler(index_);
        handler.autoUnescapeCharacters = false;
        SAXParser::parse(*is_, handler);
    }

    createMaps();
}


void SpectrumList_mzMLImpl::createMaps() const
{
    idToIndex_.clear();
    spotIDToIndexList_.clear();

    vector<SpectrumIdentity>::const_iterator it;
    it=index_.begin();
    for (size_t i=0; i!=index_.size(); ++i, ++it)
    {
        idToIndex_[it->id] = i;
        if (!it->spotID.empty())
            spotIDToIndexList_[it->spotID].push_back(i);
    }   
}


} // namespace


PWIZ_API_DECL SpectrumListPtr SpectrumList_mzML::create(shared_ptr<istream> is, const MSData& msd, bool indexed)
{
    if (!is.get() || !*is)
        throw runtime_error("[SpectrumList_mzML::create()] Bad istream.");

    return SpectrumListPtr(new SpectrumList_mzMLImpl(is, msd, indexed));
}


} // namespace msdata
} // namespace pwiz

