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

#include "ChromatogramList_mzML.hpp"
#include "IO.hpp"
#include "References.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using namespace pwiz::cv;
using boost::iostreams::offset_to_position;


namespace {


class ChromatogramList_mzMLImpl : public ChromatogramList_mzML
{
    public:

    ChromatogramList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed);

    // ChromatogramList implementation

    virtual size_t size() const {return index_.size();}
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;


    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    mutable vector<ChromatogramIdentity> index_;
    mutable map<string,size_t> idToIndex_;

    void readIndex();
    void createIndex() const;
    void createMaps() const;
};


ChromatogramList_mzMLImpl::ChromatogramList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, bool indexed)
:   is_(is), msd_(msd)
{
    if (indexed)
        try
        {
            readIndex();
        }
        catch (runtime_error&)
        {
            // TODO: log warning that the index was corrupt/missing
            createIndex();
        }
    else
        createIndex();

    createMaps();
}


const ChromatogramIdentity& ChromatogramList_mzMLImpl::chromatogramIdentity(size_t index) const
{
    if (index > index_.size())
        throw runtime_error("[ChromatogramList_mzML::chromatogramIdentity()] Index out of bounds.");

    return index_[index];
}


size_t ChromatogramList_mzMLImpl::find(const string& id) const
{
    map<string,size_t>::const_iterator it=idToIndex_.find(id);
    return it!=idToIndex_.end() ? it->second : size();
}


ChromatogramPtr ChromatogramList_mzMLImpl::chromatogram(size_t index, bool getBinaryData) const
{
    if (index >= index_.size())
        throw runtime_error("[ChromatogramList_mzML::chromatogram()] Index out of bounds.");

    // allocate Chromatogram object and read it in

    ChromatogramPtr result(new Chromatogram);
    if (!result.get())
        throw runtime_error("[ChromatogramList_mzML::chromatogram()] Out of memory.");

    IO::BinaryDataFlag binaryDataFlag = getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData;

    try
    {
        is_->seekg(offset_to_position(index_[index].sourceFilePosition));
        if (!*is_) 
            throw runtime_error("[ChromatogramList_mzML::chromatogram()] Error seeking to <chromatogram>.");

        IO::read(*is_, *result, binaryDataFlag);

        // test for reading the wrong chromatogram
        if (result->index != index)
            throw runtime_error("[ChromatogramList_mzML::chromatogram()] Index entry points to the wrong chromatogram.");
    }
    catch (runtime_error&)
    {
        // TODO: log warning about missing/corrupt index

        // recreate index
        createIndex();

        is_->seekg(offset_to_position(index_[index].sourceFilePosition));
        IO::read(*is_, *result, binaryDataFlag);
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
            throw runtime_error(("[ChromatogramList_mzML::HandlerIndexListOffset] Unexpected element name: " + name).c_str());
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
    ChromatogramIdentity* chromatogramIdentity; 

    HandlerOffset() : chromatogramIdentity(0) {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!chromatogramIdentity)
            throw runtime_error("[ChromatogramList_mzML::HandlerOffset] Null chromatogramIdentity."); 

        if (name != "offset")
            throw runtime_error(("[ChromatogramList_mzML::HandlerOffset] Unexpected element name: " + name).c_str());

        getAttribute(attributes, "idRef", chromatogramIdentity->id);

        return Status::Ok;
    }

    virtual Status characters(const string& text,
                              stream_offset position)
    {
        if (!chromatogramIdentity)
            throw runtime_error("[ChromatogramList_mzML::HandlerOffset] Null chromatogramIdentity."); 

        chromatogramIdentity->sourceFilePosition = lexical_cast<stream_offset>(text);
        return Status::Ok;
    }
};


class HandlerIndex : public SAXParser::Handler
{
    public:

    HandlerIndex(vector<ChromatogramIdentity>& index)
    :   index_(index), isChromatogramIndex_(false)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "index")
        {
            string name;
            getAttribute(attributes, "name", name);
            if (name == "chromatogram") isChromatogramIndex_ = true;
            return Status::Ok;
        }
        else if (name == "offset")
        {
            if (!isChromatogramIndex_) return Status::Ok;
            index_.push_back(ChromatogramIdentity());
            index_.back().index = index_.size()-1;
            handlerOffset_.chromatogramIdentity = &index_.back();
            return Status(Status::Delegate, &handlerOffset_);
        }
        else if (name == "indexOffset")
        {
            // hack: abort if we've reached <indexOffset> (i.e. no chromatogram index encoded) 
            return Status::Done; 
        }
        else
        {
            throw runtime_error(("[ChromatogramList_mzML::HandlerIndex] Unexpected element name: " + name).c_str());
        }
    }

    private:
    vector<ChromatogramIdentity>& index_;
    HandlerOffset handlerOffset_;
    bool isChromatogramIndex_;
};


class HandlerIndexList : public SAXParser::Handler
{
    public:

    HandlerIndexList(vector<ChromatogramIdentity>& index)
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
            if (indexName == "chromatogram")
            {    
                return Status(Status::Delegate, &handlerIndex_);
            }
            else
            {
                return Status(Status::Delegate, &dummy_);
            }
        }
        else
            throw runtime_error(("[ChromatogramList_mzML::HandlerIndex] Unexpected element name: " + name).c_str());
    }

    private:
    HandlerIndex handlerIndex_;
    SAXParser::Handler dummy_;
};


void ChromatogramList_mzMLImpl::readIndex()
{
    // find <indexOffset>

    const int bufferSize = 512;
    string buffer(bufferSize, '\0');

    is_->clear();
    is_->seekg(-bufferSize, ios::end);
    is_->read(&buffer[0], bufferSize);

    string::size_type indexIndexListOffset = buffer.find("<indexListOffset>");
    if (indexIndexListOffset == string::npos)
        throw runtime_error("ChromatogramList_mzML::readIndex()] <indexListOffset> not found."); 

    is_->seekg(-bufferSize + static_cast<int>(indexIndexListOffset), ios::end);
    if (!*is_)
        throw runtime_error("ChromatogramList_mzML::readIndex()] Error seeking to <indexListOffset>."); 
    
    // read <indexListOffset>

    boost::iostreams::stream_offset indexListOffset = 0;
    HandlerIndexListOffset handlerIndexListOffset(indexListOffset);
    SAXParser::parse(*is_, handlerIndexListOffset);
    if (indexListOffset == 0)
        throw runtime_error("ChromatogramList_mzML::readIndex()] Error parsing <indexListOffset>."); 

    // read <index>

    is_->seekg(offset_to_position(indexListOffset));
    if (!*is_) 
        throw runtime_error("[ChromatogramList_mzML::readIndex()] Error seeking to <index>.");

    HandlerIndexList handlerIndexList(index_);
    SAXParser::parse(*is_, handlerIndexList);
}


class HandlerIndexCreator : public SAXParser::Handler
{
    public:

    HandlerIndexCreator(vector<ChromatogramIdentity>& index)
    :   index_(index), chromatogramCount_(0)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "chromatogram")
        {
            index_.push_back(ChromatogramIdentity());
            ChromatogramIdentity& ci = index_.back();
            ci.index = chromatogramCount_;
            ci.sourceFilePosition = position;

            getAttribute(attributes, "id", ci.id);

            ++chromatogramCount_;
        }

        return Status::Ok;
    }

    virtual Status endElement(const string& name, 
                              stream_offset position)
    {
        if (name == "chromatogramList")
            return Status::Done;

        return Status::Ok;
    }

    private:
    vector<ChromatogramIdentity>& index_;
    size_t chromatogramCount_;
};


void ChromatogramList_mzMLImpl::createIndex() const
{
    is_->clear();
    is_->seekg(0);
    index_.clear();
    HandlerIndexCreator handler(index_);
    SAXParser::parse(*is_, handler);
}


void ChromatogramList_mzMLImpl::createMaps() const
{
    idToIndex_.clear();

    vector<ChromatogramIdentity>::const_iterator it=index_.begin();
    for (size_t i=0; i!=index_.size(); ++i, ++it)
        idToIndex_[it->id] = i;
}


} // namespace


PWIZ_API_DECL ChromatogramListPtr ChromatogramList_mzML::create(shared_ptr<istream> is, const MSData& msd, bool indexed)
{
    if (!is.get() || !*is)
        throw runtime_error("[ChromatogramList_mzML::create()] Bad istream.");

    return ChromatogramListPtr(new ChromatogramList_mzMLImpl(is, msd, indexed));
}


} // namespace msdata
} // namespace pwiz

