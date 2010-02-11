//
// $Id$
//
//
// Original author: Barbara Frewen <ferwen@u.washington.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Serializer_MSn.hpp"
#include "SpectrumList_MSn.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/foreach.hpp"
#include "boost/algorithm/string/join.hpp"


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::iostreams::stream_offset;
using namespace pwiz::util;


class Serializer_MSn::Impl
{
    public:

  Impl(MSn_Type filetype)
    : _filetype(filetype)
  {}

    void write(ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;

    void read(shared_ptr<istream> is, MSData& msd) const;

    private: 
  MSn_Type _filetype; // .ms2, .cms2, .bms2
};


void Serializer_MSn::Impl::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
  throw runtime_error("[Serializer_MSn::write()] File writing not implemented for MSn files.");
}


void Serializer_MSn::Impl::read(shared_ptr<istream> is, MSData& msd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_MSn::read()] Bad istream.");

    is->seekg(0);

    msd.fileDescription.fileContent.set(MS_MSn_spectrum);
    msd.fileDescription.fileContent.set(MS_centroid_spectrum);
    msd.fileDescription.fileContent.set(MS_scan_number_only_nativeID_format);
    msd.run.spectrumListPtr = SpectrumList_MSn::create(is, msd, _filetype);
    msd.run.chromatogramListPtr.reset(new ChromatogramListSimple);

}


//
// Serializer_MSn
//

PWIZ_API_DECL Serializer_MSn::Serializer_MSn(MSn_Type filetype)
:   impl_(new Impl(filetype))
{}

PWIZ_API_DECL void Serializer_MSn::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
  
{
    return impl_->write(os, msd, iterationListenerRegistry);
}


PWIZ_API_DECL void Serializer_MSn::read(shared_ptr<istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


} // namespace msdata
} // namespace pwiz

