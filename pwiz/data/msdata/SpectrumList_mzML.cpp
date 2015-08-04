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
#include "pwiz/utility/misc/Std.hpp"
#include <boost/bind.hpp>
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;
using boost::iostreams::offset_to_position;


namespace {


class SpectrumList_mzMLImpl : public SpectrumList_mzML
{
    public:

    SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& index);

    // SpectrumList implementation

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual IndexList findSpotID(const std::string& spotID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;
    virtual SpectrumPtr spectrum(const SpectrumPtr &seed, bool getBinaryData) const;
    virtual SpectrumPtr spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, const SpectrumPtr *defaults) const;

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    int schemaVersion_;
    mutable bool indexed_;
    mutable boost::mutex readMutex;

    Index_mzML_Ptr index_;
};


SpectrumList_mzMLImpl::SpectrumList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& index)
:   is_(is), msd_(msd), index_(index)
{
    schemaVersion_ = bal::starts_with(msd_.version(), "1.0") ? 1 : 0;
}


size_t SpectrumList_mzMLImpl::size() const
{
    //boost::call_once(indexSizeSet_.flag, boost::bind(&SpectrumList_mzMLImpl::setIndexSize, this));
    return index_->spectrumCount();
}


const SpectrumIdentity& SpectrumList_mzMLImpl::spectrumIdentity(size_t index) const
{
    //boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    if (index >= index_->spectrumCount())
        throw runtime_error("[SpectrumList_mzML::spectrumIdentity()] Index out of bounds.");

    return index_->spectrumIdentity(index);
}


size_t SpectrumList_mzMLImpl::find(const string& id) const
{
    //boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    return index_->findSpectrumId(id);
}


IndexList SpectrumList_mzMLImpl::findSpotID(const string& spotID) const
{
    //boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    return index_->findSpectrumBySpotID(spotID);
}

SpectrumPtr SpectrumList_mzMLImpl::spectrum(size_t index, DetailLevel detailLevel) const
{
    return spectrum(index, (detailLevel == DetailLevel_FullData) ? IO::ReadBinaryData : IO::IgnoreBinaryData, NULL);
}

SpectrumPtr SpectrumList_mzMLImpl::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData, NULL);
}

/// get a copy of the seed spectrum with its binary data populated
/// this is useful for formats like mzML that can delay loading of binary data
/// - client may assume the underlying Spectrum* is valid 
SpectrumPtr SpectrumList_mzMLImpl::spectrum(const SpectrumPtr &seed, bool getBinaryData) const {
    return spectrum(seed->index, getBinaryData ? IO::ReadBinaryDataOnly: IO::IgnoreBinaryData, &seed);
}

SpectrumPtr SpectrumList_mzMLImpl::spectrum(size_t index, IO::BinaryDataFlag binaryDataFlag, const SpectrumPtr *defaults) const
{
    boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
    //boost::call_once(indexInitialized_.flag, boost::bind(&SpectrumList_mzMLImpl::createIndex, this));
    if (index >= index_->spectrumCount())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Index out of bounds.");

    // allocate Spectrum object and read it in

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_mzML::spectrum()] Out of memory.");
    if (defaults) { // provide some context from previous parser runs
        result = *defaults; // copy in anything we may have cached before
    }

    try
    {
        // we may just be here to get binary data of otherwise previously read spectrum
        const SpectrumIdentityFromXML &id = index_->spectrumIdentity(index);
        boost::iostreams::stream_offset seekto =
            binaryDataFlag==IO::ReadBinaryDataOnly ? 
            id.sourceFilePositionForBinarySpectrumData : // might be set, might be -1
            (boost::iostreams::stream_offset)-1;
        if (seekto == (boost::iostreams::stream_offset)-1) {
            seekto = id.sourceFilePosition;
        }
        is_->seekg(offset_to_position(seekto));
        if (!*is_) 
            throw runtime_error("[SpectrumList_mzML::spectrum()] Error seeking to <spectrum>.");

        IO::read(*is_, *result, binaryDataFlag, schemaVersion_, &index_->legacyIdRefToNativeId(), &msd_, &id);

        // test for reading the wrong spectrum
        if (result->index != index)
            throw runtime_error("[SpectrumList_mzML::spectrum()] Index entry points to the wrong spectrum.");
    }
    catch (runtime_error&)
    {
        // TODO: log warning about missing/corrupt index

        // recreate index
        indexed_ = false;
        index_->recreate();
        const SpectrumIdentityFromXML &id = index_->spectrumIdentity(index);
        is_->seekg(offset_to_position(id.sourceFilePosition));
        IO::read(*is_, *result, binaryDataFlag, schemaVersion_, &index_->legacyIdRefToNativeId(), &msd_, &id);
    }

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

    return result;
}

} // namespace


PWIZ_API_DECL SpectrumListPtr SpectrumList_mzML::create(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& indexPtr)
{
    if (!is.get() || !*is)
        throw runtime_error("[SpectrumList_mzML::create()] Bad istream.");

    return SpectrumListPtr(new SpectrumList_mzMLImpl(is, msd, indexPtr));
}


} // namespace msdata
} // namespace pwiz

