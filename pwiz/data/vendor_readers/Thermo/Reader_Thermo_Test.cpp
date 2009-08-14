//
// Reader_Thermo_Test.cpp
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


#include "Reader_Thermo.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"

struct IsRawFile : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bal::to_lower_copy(bfs::path(rawpath).extension()) == ".raw";
    }
};

int main(int argc, char* argv[])
{
    #if defined(PWIZ_READER_THERMO) && !defined(PWIZ_READER_THERMO_TEST_ACCEPT_ONLY)
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        return pwiz::util::testReader(pwiz::msdata::Reader_Thermo(),
                                      vector<string>(argv, argv+argc),
                                      testAcceptOnly,
                                      IsRawFile());
    }
    catch (std::runtime_error& e)
    {
        cerr << "Unit test " << __FILE__ << " failed because:\n" << e.what() << endl;
        return 1;
    }
}
