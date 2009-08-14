//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "Reader_Bruker.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"

struct IsDirectory : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bfs::is_directory(rawpath);
    }
};

int main(int argc, char* argv[])
{
    #ifdef PWIZ_READER_BRUKER
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        return pwiz::util::testReader(pwiz::msdata::Reader_Bruker(),
                                      vector<string>(argv, argv+argc),
                                      testAcceptOnly,
                                      IsDirectory());
    }
    catch (std::runtime_error& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
