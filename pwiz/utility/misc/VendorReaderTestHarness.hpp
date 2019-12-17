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
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include <boost/optional.hpp>
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

struct PWIZ_API_DECL IsNamedRawFile : public TestPathPredicate
{
    IsNamedRawFile(const std::string& rawpath) : filenames({ rawpath }) {}
    IsNamedRawFile(const std::initializer_list<std::string>& filenames) : filenames(filenames) {}

    bool operator() (const std::string& rawpath) const
    {
        return filenames.count(bfs::path(rawpath).filename().string()) > 0;
    }

    std::set<std::string> filenames;
};

struct PWIZ_API_DECL ReaderTestConfig : public pwiz::msdata::Reader::Config
{
    ReaderTestConfig() : peakPicking(false), peakPickingCWT(false), thresholdCount(0), doublePrecision(false), autoTest(false) {}
    ReaderTestConfig(const ReaderTestConfig& rhs) : pwiz::msdata::Reader::Config(rhs)
    {
        peakPicking = rhs.peakPicking;
        peakPickingCWT = rhs.peakPickingCWT;
        thresholdCount = rhs.thresholdCount;
        doublePrecision = rhs.doublePrecision;
        diffPrecision = rhs.diffPrecision;
        indexRange = rhs.indexRange;
        autoTest = rhs.autoTest;
    }

    std::string resultFilename(const std::string& baseFilename) const;
    void wrap(pwiz::msdata::MSData& msd) const;

    bool peakPicking; // test vendor centroiding
    bool peakPickingCWT; // test CWT centroiding (happens automatically with vendor centroiding, but only to check for BinaryData bugs)
    int thresholdCount; // test that downstream mutating filters don't conflict with any vendor reader implementation details
    bool doublePrecision; // true if vendor data needs 64-bit precision (like Bruker TDF)
    boost::optional<double> diffPrecision; // override default Diff::BaseDiffConfig::precision
    boost::optional<std::pair<int, int>> indexRange;

    bool autoTest; // test config variant generated automatically (e.g. thresholdCount) that does not use a separate mzML file
};

/// A common test harness for vendor readers; returns pair(failedTests, totalTests)
PWIZ_API_DECL
std::pair<int, int> testReader(const pwiz::msdata::Reader& reader,
                               const std::vector<std::string>& args,
                               bool testAcceptOnly, bool requireUnicodeSupport,
                               const TestPathPredicate& isPathTestable,
                               const ReaderTestConfig& config = ReaderTestConfig());


} // namespace util
} // namespace pwiz
