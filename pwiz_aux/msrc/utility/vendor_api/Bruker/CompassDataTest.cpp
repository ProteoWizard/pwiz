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


#include "CompassData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::vendor_api::Bruker;


ostream* os_ = 0;


void test(const string& rawpath)
{
    cout << "Testing " << rawpath << endl;
    CompassDataPtr compassData = CompassData::create(rawpath);
    cout << compassData->getAnalysisName() << endl;
    cout << compassData->getAnalysisDateTime().to_string() << endl;
    cout << compassData->getSampleName() << endl;
    cout << compassData->getOperatorName() << endl;
    cout << compassData->getInstrumentFamily() << endl;
    cout << compassData->getInstrumentDescription() << endl;
    cout << compassData->getMSSpectrumCount() << endl;

    if (compassData->getMSSpectrumCount() == 0)
        throw runtime_error("[CompassDataTest] No spectra detected in test data at: " + rawpath);

    MSSpectrumPtr s1 = compassData->getMSSpectrum(1);
    cout << s1->getProfileDataSize() << " " << s1->getLineDataSize() << endl;
    automation_vector<double> masses, intensities;
    s1->getProfileData(masses, intensities);
    if (!masses.empty())
        cout << "Masses (" << masses.size() << ") [" << masses.front() << "-" << masses.back() << "]" << endl;

    for (size_t i=1, end=compassData->getMSSpectrumCount(); i <= end; ++i)
    {
        MSSpectrumPtr sMSMS = compassData->getMSSpectrum(i);
        if (sMSMS->getMSMSStage() == 1)
            continue;

        std::vector<double> mzs;
        std::vector<IsolationMode> isolationModes;
        sMSMS->getIsolationData(mzs, isolationModes);
        if (!mzs.empty())
            cout << "Masses (" << mzs.size() << ") [" << mzs.front() << "=" << isolationModes.front() << "]" << endl;

        std::vector<FragmentationMode> fragmentationModes;
        sMSMS->getFragmentationData(mzs, fragmentationModes);
        if (!mzs.empty())
            cout << "Masses (" << mzs.size() << ") [" << mzs.front() << "=" << fragmentationModes.front() << "]" << endl;
        break;
    }
}


int main(int argc, char* argv[])
{
    try
    {
        vector<string> rawpathmasks;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else rawpathmasks.push_back(argv[i]);
        }

        vector<string> args(argv, argv+argc);
        if (rawpathmasks.empty())
            throw runtime_error(string("Invalid arguments: ") + bal::join(args, " ") +
                                "\nUsage: CompassDataTest [-v] <source pathmask 1> [source pathmask 2] ..."); 

        for (size_t i=0; i < rawpathmasks.size(); ++i)
        {
            vector<bfs::path> rawpaths;
            expand_pathmask(rawpathmasks[i], rawpaths);
            for (size_t j=0; j < rawpaths.size(); ++j)
                test(rawpaths[j].string());
        }
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
