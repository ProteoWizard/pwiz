//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "RawData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::Waters;


ostream* os_ = 0;


void test(const string& rawpath)
{
    // TODO: get some example Waters RAW sources
    RawDataPtr rawData = RawData::create(rawpath);

    for (FunctionList::const_iterator itr = rawData->functions().begin();
         itr != rawData->functions().end();
         ++itr)
    {
        FunctionPtr functionPtr = *itr;
        size_t scanCount = functionPtr->getScanCount();
        cout << "Function " << functionPtr->getFunctionNumber()
             << " (type " << functionPtr->getFunctionType() << ", "
             << scanCount << " scans)" << endl;

        size_t SRMSize = functionPtr->getSRMSize();
        if (SRMSize > 0)
        {
            for (size_t i=0; i < SRMSize; ++i)
            {
                SRMTarget target;
                functionPtr->getSRM(i, target);
                cout << "MRM target: " << target.Q1 << "->" << target.Q3 << endl;
            }
        }
        else
        {
            for (size_t i=1; i <= scanCount; ++i)
            {
                ScanPtr scanPtr = functionPtr->getScan(0, i);
                cout << "Scan " << scanPtr->getScanNumber()
                    << " (" << scanPtr->getNumPoints() << " points)" << endl;
            }
        }
    }
}


int main(int argc, char* argv[])
{
    try
    {
        vector<string> rawpaths;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpaths.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpaths.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: RawDataTest [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            test(rawpaths[i]);
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}
