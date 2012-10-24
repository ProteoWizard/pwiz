//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "MascotReader.hpp"
#include <iostream>
#include <stdexcept>

namespace pwiz {
namespace identdata {

using namespace std;

PWIZ_API_DECL MascotReader::MascotReader()
{
}

PWIZ_API_DECL std::string MascotReader::identify(const std::string& filename,
                                   const std::string& head) const
{
    return "";
}

PWIZ_API_DECL void MascotReader::read(const std::string& filename,
                        const std::string& head,
                        IdentData& result,
                        const Reader::Config& config) const
{
    throw runtime_error("[MascotReader::identify] no mascot support enabled.");
}

PWIZ_API_DECL void MascotReader::read(const std::string& filename,
                        const std::string& head,
                        IdentDataPtr& result,
                        const Reader::Config& config) const
{
    throw runtime_error("[MascotReader::read] no mascot support enabled.");
}

PWIZ_API_DECL void MascotReader::read(const std::string& filename,
                        const std::string& head,
                        std::vector<IdentDataPtr>& results,
                        const Reader::Config& config) const
{
    throw runtime_error("[MascotReader::read] no mascot support enabled.");
}

PWIZ_API_DECL const char *MascotReader::getType() const
{
    return "mzIdentML";
}

class MascotReader::Impl
{
    Impl() {}
    ~Impl() {}
};

} // namespace pwiz 
} // namespace identdata 
