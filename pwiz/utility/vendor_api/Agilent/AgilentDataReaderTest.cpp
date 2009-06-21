//
// AgilentDataReader.cpp
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


#include "AgilentDataReader.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::agilent;


ostream* os_ = 0;


void test(const string& rawpath)
{
    // TODO: get some example Agilent data files
    AgilentDataReaderPtr dataReader = AgilentDataReader::create(rawpath);

/*
    TODO(bmaclean): Write some real test code.  Below is a snip from the .wiff file test

    double start, stop;
    msExperiment->getAcquisitionMassRange(start, stop);
    cout << start << " - " << stop << endl;
    cout << (int) msExperiment->getPolarity() << endl;
    cout << (int) msExperiment->getScanType() << endl;
    vector<double> times, intensities;
    msExperiment->getTIC(times, intensities);
    cout << times.size() << endl;
    int endCycle = wiffFile->getCycleCount(sample, period, experiment);
    double startTime = msExperiment->getCycleStartTime(1);
    double stopTime = msExperiment->getCycleStartTime(endCycle);
    cout << startTime << " - " << stopTime << endl;

    cout << msExperiment->getSRMSize() << endl;
    if(msExperiment->getSRMSize() > 0)
    {
        Target target;
        ostringstream oss;
        for(size_t i=0; i < msExperiment->getSRMSize(); ++i)
        {
            msExperiment->getSRM(i, target);
            oss << target.Q1 << "->" << target.Q3 <<
                   " CE=" << target.collisionEnergy <<
                   " DP=" << target.declusteringPotential <<
                   " DT=" << target.dwellTime << "; ";

            msExperiment->getSIC(i, times, intensities);
            if(!intensities.empty())
                cout << times.front() << ":" << intensities.front() << " - " << times.back() << ":" << intensities.back() << endl;
        }
        cout << oss.str() << endl;
    }

    msExperiment->getTIC(times, intensities);
    if(!intensities.empty())
        cout << times.front() << ":" << intensities.front() << " - " << times.back() << ":" << intensities.back() << endl;

    SpectrumPtr spectrum = wiffFile->getSpectrum(msExperiment, endCycle);
    cout << spectrum->getStartTime() << endl;
    cout << spectrum->getDataSize(false) << endl;
    cout << spectrum->getDataSize(true) << endl;
    vector<double> mz;
    spectrum->getData(false, mz, intensities);
    if(!intensities.empty())
        cout << mz.front() << ":" << intensities.front() << " - " << mz.back() << ":" << intensities.back() << endl;
*/
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
                                "\nUsage: AgilentDataReaderTest [-v] <source path 1> [source path 2] ..."); 

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
