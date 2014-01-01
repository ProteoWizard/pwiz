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

    ChromatogramList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& index);

    // ChromatogramList implementation

    virtual size_t size() const {return index_->chromatogramCount();}
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const std::string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;


    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    Index_mzML_Ptr index_;
};


ChromatogramList_mzMLImpl::ChromatogramList_mzMLImpl(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& index)
:   is_(is), msd_(msd), index_(index)
{
}


const ChromatogramIdentity& ChromatogramList_mzMLImpl::chromatogramIdentity(size_t index) const
{
    if (index >= index_->chromatogramCount())
        throw runtime_error("[ChromatogramList_mzML::chromatogramIdentity()] Index out of bounds.");

    return index_->chromatogramIdentity(index);
}


size_t ChromatogramList_mzMLImpl::find(const string& id) const
{
    return index_->findChromatogramId(id);
}


ChromatogramPtr ChromatogramList_mzMLImpl::chromatogram(size_t index, bool getBinaryData) const
{
    if (index >= index_->chromatogramCount())
        throw runtime_error("[ChromatogramList_mzML::chromatogram()] Index out of bounds.");

    // allocate Chromatogram object and read it in

    ChromatogramPtr result(new Chromatogram);
    if (!result.get())
        throw runtime_error("[ChromatogramList_mzML::chromatogram()] Out of memory.");

    IO::BinaryDataFlag binaryDataFlag = getBinaryData ? IO::ReadBinaryData : IO::IgnoreBinaryData;

    try
    {
        is_->seekg(offset_to_position(index_->chromatogramIdentity(index).sourceFilePosition));
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
        index_->recreate();

        is_->seekg(offset_to_position(index_->chromatogramIdentity(index).sourceFilePosition));
        IO::read(*is_, *result, binaryDataFlag);
    }

    // resolve any references into the MSData object

    References::resolve(*result, msd_);

    return result;
}

} // namespace


PWIZ_API_DECL ChromatogramListPtr ChromatogramList_mzML::create(shared_ptr<istream> is, const MSData& msd, const Index_mzML_Ptr& indexPtr)
{
    if (!is.get() || !*is)
        throw runtime_error("[ChromatogramList_mzML::create()] Bad istream.");

    return ChromatogramListPtr(new ChromatogramList_mzMLImpl(is, msd, indexPtr));
}


} // namespace msdata
} // namespace pwiz

