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


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "Reader_Bruker.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"

struct IsDirectory : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath) && !bal::icontains(rawpath, "diapasef"); // don't want default mzML conversion of diaPASEF, too big
    }
};

struct IsTDF : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::exists(bfs::path(rawpath) / "analysis.tdf") && !bal::icontains(rawpath, "diapasef"); // don't want default TDF treatment for diaPASEF, too big
    }
};

struct IsPASEF : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return IsTDF()(rawpath) && bal::icontains(rawpath, "hela_qc_pasef");
    }
};

struct IsDiaPASEF : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath) && bal::icontains(rawpath, "diapasef");
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_BRUKER
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        bool requireUnicodeSupport = false;

        pwiz::util::ReaderTestConfig config;
        config.sortAndJitter = true;

        pwiz::msdata::Reader_Bruker_BAF reader; // actually handles all file types
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsDirectory(), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile("Hela_QC_PASEF_Slot1-first-6-frames.d"), newConfig);

        config.doublePrecision = true;
        config.preferOnlyMsLevel = 1;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsTDF(), config);

        config.preferOnlyMsLevel = 2;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsTDF(), config);

        config.allowMsMsWithoutPrecursor = false;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsPASEF(), config);

        config.allowMsMsWithoutPrecursor = true; // has no effect in combined mode
        config.combineIonMobilitySpectra = true;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsTDF(), config);

        config.preferOnlyMsLevel = 1;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsTDF(), config);

        config.preferOnlyMsLevel = 0;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsTDF(), config);

        config.preferOnlyMsLevel = 2;
        config.combineIonMobilitySpectra = false;
        config.allowMsMsWithoutPrecursor = false;
        /*config.isolationMzAndMobilityFilter.emplace_back(1222);
        config.isolationMzAndMobilityFilter.emplace_back(1318);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsPASEF(), config);*/

        config.isolationMzAndMobilityFilter.clear();
        config.isolationMzAndMobilityFilter.emplace_back(895.9496, 1.12, 0.055);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsDiaPASEF(), config);
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
