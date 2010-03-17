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


#include "WiffFile.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::vendor_api::ABI;


ostream* os_ = 0;


void test(const string& rawpath)
{
    // TODO: get some example WIFFs
    WiffFilePtr wiffFile = WiffFile::create(rawpath);

    int sampleCount = wiffFile->getSampleCount();
    for (int sample=1; sample <= sampleCount; ++sample)
    {
        cout << "Sample " << sample << " (acquired " << wiffFile->getSampleAcquisitionTime().to_string() << ")" << endl;
        int periodCount = wiffFile->getPeriodCount(sample);
        for (int period=1; period <= periodCount; ++period)
        {
            cout << "Period " << period << endl;
            int experimentCount = wiffFile->getExperimentCount(sample, period);
            for (int experiment=1; experiment <= experimentCount; ++experiment)
            {
                ExperimentPtr msExperiment = wiffFile->getExperiment(sample, period, experiment);
                cout << "Experiment " << msExperiment->getExperimentNumber() << endl;
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
                                "\nUsage: WiffFileTest [-v] <source path 1> [source path 2] ..."); 

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
