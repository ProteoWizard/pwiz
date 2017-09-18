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


#include "UnifiData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::UNIFI;


ostream* os_ = 0;


void test(const string& rawpath)
{
    cout << "Opening " << rawpath << endl;
    UnifiData data(rawpath);

    cout << "AcquisitionStartTime: " << data.getAcquisitionStartTime() << endl;
    cout << "Sample name: " << data.getSampleName() << endl;
    cout << "Sample description: " << data.getSampleDescription() << endl;

    std::vector<double> mz, intensity;
    for (size_t i = 0; i < min((size_t) 1000u, data.numberOfSpectra()); i += 1)
    {
        data.getSpectrum(i, mz, intensity);
        cout << "Spectrum " << i << " MZs: [" << mz.front() << ", " << mz.back() << "], Intensities: [" << intensity.front() << ", " << intensity.back() << "]" << endl;
        //_sleep(1000);
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
                                "\nUsage: UnifiDataTest [-v] <sampleResult URL 1> [sampleResult URL 2] ..."); 

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
