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


#ifndef _XMLMETADATAPARSER_HPP_
#define _XMLMETADATAPARSER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <map>
#include <vector>

namespace pwiz {
namespace vendor_api {
namespace Agilent {

struct PWIZ_API_DECL Device
{
    int DeviceID;
    std::string Name;
    std::string DriverVersion;
    std::string FirmwareVersion;
    std::string ModelNumber;
    std::string OrdinalNumber;
    std::string SerialNumber;
    std::string Type;
    std::string StoredDataType;
    std::string Delay;
    std::string Vendor;
};


class PWIZ_API_DECL XmlMetadataParser
{
    public:
    explicit XmlMetadataParser(const std::string& acqDataPath,
                               std::map<std::string, std::string>& sampleInfoMap,
                               std::vector<Device>& devices);
    void parse(); // throws on error

    private:
    std::string acqDataPath_;
    std::map<std::string, std::string>& sampleInfoMap_;
    std::vector<Device>& devices_;
};

} // namespace Agilent
} // namespace vendor_api
} // namespace pwiz

#endif // _XMLMETADATAPARSER_HPP_
