//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2026 University of Washington
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


#ifndef _SAMPLEINFOPARSER_HPP_
#define _SAMPLEINFOPARSER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <map>

namespace pwiz {
namespace vendor_api {
namespace Agilent {

class PWIZ_API_DECL SampleInfoParser
{
    public:
    explicit SampleInfoParser(const std::string& filepath, std::map<std::string, std::string>& sampleInfoMap);
    void parse(); // throws on error

    private:
    std::string filepath_;
    std::map<std::string, std::string>& map_;
};

} // namespace Agilent
} // namespace vendor_api
} // namespace pwiz

#endif // _SAMPLEINFOPARSER_HPP_
