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


#ifndef _READER_WATERS_HPP_
#define _READER_WATERS_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/Reader.hpp"


// DAC usage is msvc only - mingw doesn't provide com support
#if (!defined(_MSC_VER) && defined(PWIZ_READER_WATERS))
#undef PWIZ_READER_WATERS
#endif


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_Waters : public Reader
{
    public:

	virtual std::string identify(const std::string& filename,
                        const std::string& head) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back(), 0, config);
    }

    virtual const char * getType() const {return "Waters RAW";}
    virtual CVID getCvType() const {return MS_Waters_raw_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".raw"};}
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_WATERS_HPP_
