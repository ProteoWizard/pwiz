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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <string>
#include <vector>


namespace pwiz {
namespace util {


/// test implementations derive from this to define which paths should be tested
struct PWIZ_API_DECL TestPathPredicate
{
    /// returns true iff the given rawpath is a real path to test/generate
    virtual bool operator() (const std::string& rawpath) const = 0;

    virtual ~TestPathPredicate() {}
};


struct PWIZ_API_DECL ReaderTestConfig : public pwiz::msdata::Reader::Config
{
    std::string resultFilename(const std::string& baseFilename) const;
};

/// A common test harness for vendor readers;
PWIZ_API_DECL
int testReader(const pwiz::msdata::Reader& reader,
               const std::vector<std::string>& args,
               bool testAcceptOnly, bool requireUnicodeSupport,
               const TestPathPredicate& isPathTestable,
               const ReaderTestConfig& config = ReaderTestConfig());


} // namespace util
} // namespace pwiz
