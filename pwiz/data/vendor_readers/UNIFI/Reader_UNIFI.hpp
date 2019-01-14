//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _READER_UNIFI_HPP_
#define _READER_UNIFI_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/shared_ptr.hpp>


// UNIFI usage is .NET for now
#if (!defined(_MSC_VER) && defined(PWIZ_READER_UNIFI))
#undef PWIZ_READER_UNIFI
#endif


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_UNIFI : public Reader
{
    public:

	virtual std::string identify(const std::string& filename,
                        const std::string& head) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      MSData& result,
                      int sampleIndex = 0,
                      const Config& config = Config()) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const;

    virtual void readIds(const std::string& filename,
                      const std::string& head,
                      std::vector<std::string>& results,
                      const Config& config = Config()) const;

    virtual const char * getType() const {return "Waters UNIFI";}
    virtual CVID getCvType() const {return MS_Waters_raw_format;}
    virtual std::vector<std::string> getFileExtensions() const {return { };}
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_UNIFI_HPP_
