//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

#include "Index_mzML.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/data/msdata/IO.hpp"

using namespace pwiz::util;
using namespace pwiz::minimxml;
using boost::iostreams::offset_to_position;


namespace pwiz {
namespace msdata {


struct Index_mzML::Impl
{
    Impl(const boost::shared_ptr<std::istream>& is, int schemaVersion)
        : is_(is), schemaVersion_(schemaVersion),
          spectrumCount_(0), chromatogramCount_(0)
    {
        createIndex();
    }

    void recreate() const;
    void readIndex() const;
    void createIndex() const;
    void createMaps() const;

    boost::shared_ptr<std::istream> is_;
    int schemaVersion_;

    mutable size_t spectrumCount_;
    mutable vector<SpectrumIdentityFromXML> spectrumIndex_;
    mutable map<string,size_t> spectrumIdToIndex_;
    mutable map<string,IndexList> spotIDToIndexList_;
    mutable map<string,string> legacyIdRefToNativeId_;

    mutable size_t chromatogramCount_;
    mutable vector<ChromatogramIdentity> chromatogramIndex_;
    mutable map<string,size_t> chromatogramIdToIndex_;
};


namespace {

class HandlerIndexListOffset : public SAXParser::Handler
{
    public:

    HandlerIndexListOffset(stream_offset& indexListOffset)
    :   indexListOffset_(indexListOffset)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name != "indexListOffset")
            throw runtime_error("[Index_mzML::HandlerIndexOffset] Unexpected element name: " + name);
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text,
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
    ChromatogramIdentity* chromatogramIdentity;
    map<string,string>* legacyIdRefToNativeId;

    HandlerOffset() : spectrumIdentity(0), chromatogramIdentity(0)
    {
        parseCharacters = true;
        autoUnescapeCharacters = false;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {

        if (name != "offset")
            throw runtime_error("[Index_mzML::HandlerOffset] Unexpected element name: " + name);

        if (spectrumIdentity)
        {
            getAttribute(attributes, "idRef", spectrumIdentity->id);
            getAttribute(attributes, "spotID", spectrumIdentity->spotID);

            // mzML 1.0
            if (version == 1)
            {
                string idRef, nativeID;
                getAttribute(attributes, "idRef", idRef);
                getAttribute(attributes, "nativeID", nativeID);
                if (nativeID.empty())
                    spectrumIdentity->id = idRef;
                else
                {
                    try
                    {
                        lexical_cast<int>(nativeID);
                        spectrumIdentity->id = "scan=" + nativeID;
                    }
                    catch(exception&)
                    {
                        spectrumIdentity->id = nativeID;
                    }
                    (*legacyIdRefToNativeId)[idRef] = spectrumIdentity->id;
                }
            }
        }
        else if (chromatogramIdentity)
        {
            getAttribute(attributes, "idRef", chromatogramIdentity->id);
        }
        else
            throw runtime_error("[Index_mzML::HandlerOffset] Null identity."); 

        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position)
    {
        if (spectrumIdentity)
            spectrumIdentity->sourceFilePosition = lexical_cast<stream_offset>(text);
        else if (chromatogramIdentity)
            chromatogramIdentity->sourceFilePosition = lexical_cast<stream_offset>(text);
        else
            throw runtime_error("[Index_mzML::HandlerOffset] Null identity."); 

        return Status::Ok;
    }
};


class HandlerIndexList : public SAXParser::Handler
{
    public:

    HandlerIndexList(int schemaVersion_,
                     size_t& spectrumCount,
                     vector<SpectrumIdentityFromXML>& spectrumIndex,
                     map<string,string>& legacyIdRefToNativeId,
                     size_t& chromatogramCount,
                     vector<ChromatogramIdentity>& chromatogramIndex)
    : spectrumCount_(spectrumCount),
      spectrumIndex_(spectrumIndex),
      chromatogramCount_(chromatogramCount),
      chromatogramIndex_(chromatogramIndex),
      validIndexOffset_(false)
    {
        handlerOffset_.version = schemaVersion_;
        handlerOffset_.legacyIdRefToNativeId = &legacyIdRefToNativeId;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "indexList")
        {
            validIndexOffset_ = true; // looks like indexOffset landed us in the right place
            return Status::Ok;
        }
        else if (!validIndexOffset_) // "indexList" should have been first element
            throw runtime_error("[Index_mzML::HandlerIndex] element \"indexList\" not found at expected offset - bad indexOffset value, probably");
        else if (name == "index")
        {
            string indexName;
            getAttribute(attributes, "name", indexName);
            if (indexName == "spectrum")
                inSpectrumIndex_ = true;
            else if (indexName == "chromatogram")
                inSpectrumIndex_ = false;
            else
                throw runtime_error("[Index_mzML::HandlerIndex] Unexpected index name: " + indexName);

            return Status::Ok;
        }
        else if (name == "offset")
        {
            if (inSpectrumIndex_)
            {
                handlerOffset_.chromatogramIdentity = 0;
                spectrumIndex_.push_back(SpectrumIdentityFromXML());
                handlerOffset_.spectrumIdentity = &spectrumIndex_.back();
                handlerOffset_.spectrumIdentity->index = spectrumCount_;
                ++spectrumCount_;
            }
            else
            {
                handlerOffset_.spectrumIdentity = 0;
                chromatogramIndex_.push_back(ChromatogramIdentity());
                handlerOffset_.chromatogramIdentity = &chromatogramIndex_.back();
                handlerOffset_.chromatogramIdentity->index = chromatogramCount_;
                ++chromatogramCount_;
            }
            return Status(Status::Delegate, &handlerOffset_);
        }
        else
            throw runtime_error("[Index_mzML::HandlerIndex] Unexpected element name: " + name);
    }

    private:
    size_t& spectrumCount_;
    vector<SpectrumIdentityFromXML>& spectrumIndex_;
    size_t& chromatogramCount_;
    vector<ChromatogramIdentity>& chromatogramIndex_;

    bool validIndexOffset_;
    bool inSpectrumIndex_;
    HandlerOffset handlerOffset_;
};


class HandlerIndexCreator : public SAXParser::Handler
{
    public:

    HandlerIndexCreator(int schemaVersion_,
                        size_t& spectrumCount,
                        vector<SpectrumIdentityFromXML>& spectrumIndex,
                        map<string,string>& legacyIdRefToNativeId,
                        size_t& chromatogramCount,
                        vector<ChromatogramIdentity>& chromatogramIndex)
    : spectrumCount_(spectrumCount),
      spectrumIndex_(spectrumIndex),
      chromatogramCount_(chromatogramCount),
      chromatogramIndex_(chromatogramIndex),
      legacyIdRefToNativeId(&legacyIdRefToNativeId)
    {
        version = schemaVersion_;
    }

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "spectrum")
        {
            SpectrumIdentityFromXML* si;
            spectrumIndex_.push_back(SpectrumIdentityFromXML());
            si = &spectrumIndex_.back();

            getAttribute(attributes, "id", si->id);
            getAttribute(attributes, "spotID", si->spotID);

            // mzML 1.0
            if (version == 1)
            {
                string idRef, nativeID;
                getAttribute(attributes, "id", idRef);
                getAttribute(attributes, "nativeID", nativeID);
                if (nativeID.empty())
                    si->id = idRef;
                else
                {
                    try
                    {
                        lexical_cast<int>(nativeID);
                        si->id = "scan=" + nativeID;
                    }
                    catch (exception&)
                    {
                        si->id = nativeID;
                    }
                    (*legacyIdRefToNativeId)[idRef] = si->id;
                }
            }

            si->index = spectrumCount_;
            si->sourceFilePosition = position;

            ++spectrumCount_;
        }
        else if (name == "chromatogram")
        {
            ChromatogramIdentity* ci;
            chromatogramIndex_.push_back(ChromatogramIdentity());
            ci = &chromatogramIndex_.back();

            getAttribute(attributes, "id", ci->id);

            ci->index = chromatogramCount_;
            ci->sourceFilePosition = position;

            ++chromatogramCount_;
        }
        else if (name == "indexList")
            return Status::Done;

        return Status::Ok;
    }

    private:
    size_t& spectrumCount_;
    vector<SpectrumIdentityFromXML>& spectrumIndex_;
    size_t& chromatogramCount_;
    vector<ChromatogramIdentity>& chromatogramIndex_;
    map<string, string>* legacyIdRefToNativeId;
};

} // namespace


void Index_mzML::Impl::readIndex() const
{
    // find <indexListOffset>

    const int bufferSize = 512;
    string buffer(bufferSize, '\0');

    is_->seekg(-bufferSize, std::ios::end);
    is_->read(&buffer[0], bufferSize);

    string::size_type indexIndexOffset = buffer.find("<indexListOffset>");
    if (indexIndexOffset == string::npos)
        throw index_error("Index_mzML::readIndex()] <indexListOffset> not found.");

    is_->seekg(-bufferSize + static_cast<int>(indexIndexOffset), std::ios::end);
    if (!*is_)
        throw index_error("Index_mzML::readIndex()] Error seeking to <indexListOffset>.");
    
    // read <indexListOffset>

    boost::iostreams::stream_offset indexListOffset = 0;
    HandlerIndexListOffset handlerIndexListOffset(indexListOffset);
    SAXParser::parse(*is_, handlerIndexListOffset);
    if (indexListOffset == 0)
        throw index_error("Index_mzML::readIndex()] Error parsing <indexListOffset>.");

    // read <index>

    is_->seekg(offset_to_position(indexListOffset));
    if (!*is_) 
        throw index_error("[Index_mzML::readIndex()] Error seeking to <index>.");

    HandlerIndexList handlerIndexList(schemaVersion_,
                                      spectrumCount_, spectrumIndex_, legacyIdRefToNativeId_,
                                      chromatogramCount_, chromatogramIndex_);
    SAXParser::parse(*is_, handlerIndexList);
}

void Index_mzML::Impl::createIndex() const
{
    //boost::call_once(indexSizeSet_.flag, boost::bind(&SpectrumList_mzMLImpl::setIndexSize, this));

    spectrumIndex_.clear();
    chromatogramIndex_.clear();

    // resize the index assuming the count attribute is accurate
    //index_.resize(size_);

    try
    {
        readIndex();
    }
    catch (runtime_error&)
    {
        // TODO: log warning that the index was corrupt/missing
        is_->clear();
        is_->seekg(0);
        HandlerIndexCreator handler(schemaVersion_,
                                    spectrumCount_, spectrumIndex_, legacyIdRefToNativeId_,
                                    chromatogramCount_, chromatogramIndex_);
        SAXParser::parse(*is_, handler);
    }

    createMaps();
}

void Index_mzML::Impl::createMaps() const
{
    // actually just init - build when/if actually called for
    spectrumIdToIndex_.clear();
    spotIDToIndexList_.clear();
    chromatogramIdToIndex_.clear();
}


PWIZ_API_DECL Index_mzML::Index_mzML(boost::shared_ptr<std::istream> is, const MSData& msd)
: impl_(new Impl(is, bal::starts_with(msd.version(), "1.0") ? 1 : 0))
{}

PWIZ_API_DECL void Index_mzML::recreate() {impl_->createIndex();}

PWIZ_API_DECL size_t Index_mzML::spectrumCount() const {return impl_->spectrumCount_;}
PWIZ_API_DECL const SpectrumIdentityFromXML& Index_mzML::spectrumIdentity(size_t index) const {return impl_->spectrumIndex_[index];}
PWIZ_API_DECL const map<std::string,std::string>& Index_mzML::legacyIdRefToNativeId() const {return impl_->legacyIdRefToNativeId_;}

PWIZ_API_DECL size_t Index_mzML::chromatogramCount() const {return impl_->chromatogramCount_;}
PWIZ_API_DECL const ChromatogramIdentity& Index_mzML::chromatogramIdentity(size_t index) const {return impl_->chromatogramIndex_[index];}

PWIZ_API_DECL size_t Index_mzML::findSpectrumId(const std::string& id) const
{
    if (!impl_->spectrumIdToIndex_.size()) { // first use
        BOOST_FOREACH(const SpectrumIdentityFromXML& si, impl_->spectrumIndex_)
        {
            impl_->spectrumIdToIndex_[si.id] = si.index;
        }   
    }
    map<string, size_t>::const_iterator itr = impl_->spectrumIdToIndex_.find(id);
    return itr == impl_->spectrumIdToIndex_.end() ? spectrumCount() : itr->second;
}

PWIZ_API_DECL IndexList Index_mzML::findSpectrumBySpotID(const std::string& spotID) const
{
    if (!impl_->spotIDToIndexList_.size()) { // first use
        BOOST_FOREACH(const SpectrumIdentityFromXML& si, impl_->spectrumIndex_)
        {
            if (!si.spotID.empty())
                impl_->spotIDToIndexList_[si.spotID].push_back(si.index);
        }   
    }
    map<string, IndexList>::const_iterator itr = impl_->spotIDToIndexList_.find(spotID);
    return itr == impl_->spotIDToIndexList_.end() ? IndexList() : itr->second;
}

PWIZ_API_DECL size_t Index_mzML::findChromatogramId(const std::string& id) const
{
    if (!impl_->chromatogramIdToIndex_.size()) { // first use
        BOOST_FOREACH(const ChromatogramIdentity& ci, impl_->chromatogramIndex_)
            impl_->chromatogramIdToIndex_[ci.id] = ci.index;
    }
    map<string, size_t>::const_iterator itr = impl_->chromatogramIdToIndex_.find(id);
    return itr == impl_->chromatogramIdToIndex_.end() ? chromatogramCount() : itr->second;
}


} // namespace msdata
} // namespace pwiz
