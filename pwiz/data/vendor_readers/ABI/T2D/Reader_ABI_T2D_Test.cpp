//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/unit.hpp"
#include "Reader_ABI_T2D.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

struct Is_T2D_Directory : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& datapath) const
    {
        vector<bfs::path> t2d_filepaths;
        pwiz::util::expand_pathmask(bfs::path(datapath) / "*.t2d", t2d_filepaths);
        return !t2d_filepaths.empty();
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #ifdef PWIZ_READER_ABI_T2D
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        pwiz::util::testReader(pwiz::msdata::Reader_ABI_T2D(), testArgs, testAcceptOnly, Is_T2D_Directory()).check();
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
