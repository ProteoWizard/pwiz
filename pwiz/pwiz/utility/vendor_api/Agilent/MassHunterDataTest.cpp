//
// $Id$
//
//
// Original author: Brendan MacLean <brendanx .@. u.washington.edu>
//
// Copyright 2009 University of Washington - Seattle, WA
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


#include "MassHunterData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::vendor_api::Agilent;


ostream* os_ = 0;


void test(const string& rawpath)
{
    cout << "Testing " << rawpath << endl;
    MassHunterDataPtr dataReader = MassHunterData::create(rawpath);
    cout << dataReader->getDeviceType() << endl;
    cout << dataReader->getDeviceName(dataReader->getDeviceType()) << endl;
    cout << dataReader->getAcquisitionTime().to_string() << endl;
    cout << dataReader->getIonModes() << endl;
    cout << dataReader->getScanTypes() << endl;
    cout << dataReader->getSpectraFormat() << endl;
    cout << dataReader->getTotalScansPresent() << endl;

    if (dataReader->getTotalScansPresent() == 0)
        throw runtime_error("[MassHunterDataTest] No spectra detected in test data at: " + rawpath);

    SpectrumPtr s1 = dataReader->getProfileSpectrumByRow(0);
    cout << s1->getTotalDataPoints() << endl;
    automation_vector<double> masses;
    automation_vector<float> intensities;
    s1->getXArray(masses);
    s1->getYArray(intensities);
    if (!masses.empty())
        cout << "Masses (" << masses.size() << ") [" << masses.front() << "-" << masses.back() << "]" << endl;

    MSStorageMode storageMode = s1->getMSStorageMode();
    bool hasProfile = storageMode == MSStorageMode_ProfileSpectrum ||
                      storageMode == MSStorageMode_Mixed;
    cout << storageMode << endl;
    cout << hasProfile << endl;

    if (dataReader->getScanTypes() & MSScanType_ProductIon)
    {
        for (size_t i=0, end=dataReader->getTotalScansPresent(); i < end; ++i)
        {
            SpectrumPtr sMSMS;
            if (!hasProfile)
                sMSMS = dataReader->getPeakSpectrumByRow(i);
            else
                sMSMS = dataReader->getProfileSpectrumByRow(i);
            if (!(sMSMS->getMSScanType() & MSScanType_ProductIon))
                continue;

            if (sMSMS->getParentScanId() > 0)
            {
                cout << "ParentScanId " << sMSMS->getParentScanId() << flush;
                SpectrumPtr sParent = dataReader->getProfileSpectrumById(sMSMS->getParentScanId());
                cout << " (" << sParent->getTotalDataPoints() << ")" << endl;
            }

            std::vector<double> mzs;
            sMSMS->getPrecursorIons(mzs);
            if (!mzs.empty())
                cout << "PrecursorIons (" << mzs.size() << ") [" << mzs.front() << "]" << endl;
            break;
        }
    }

    ChromatogramPtr c1 = dataReader->getChromatogram(0, ChromatogramType_TotalIon);
    c1->getXArray(masses);
    c1->getYArray(intensities);
    if (!masses.empty())
        cout << "Times (" << masses.size() << ") [" << masses.front() << "-" << masses.back() << "]" << endl;
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
                                "\nUsage: MassHunterDataTest [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            for (bfs::directory_iterator itr(rawpaths[i]); itr != bfs::directory_iterator(); ++itr)
            {
                if (itr->path().extension() == ".mzML")
                    continue;
                test(itr->path().string());
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
