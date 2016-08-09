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
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"

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
        cout << "Sample " << sample << " (acquired " << wiffFile->getSampleAcquisitionTime(sample, true).to_string() << ")" << endl;
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
                cout << "Polarity: " << (int) msExperiment->getPolarity() << endl;
                cout << "Experiment Type: " << (int) msExperiment->getExperimentType() << endl;
                cout << "Scan Type: " << (int) msExperiment->getScanType() << endl;

                vector<double> mz, times, intensities;

                int endCycle = wiffFile->getCycleCount(sample, period, experiment);
                //double startTime = msExperiment->getCycleStartTime(1);
                //double stopTime = msExperiment->getCycleStartTime(endCycle);
                //cout << startTime << " - " << stopTime << endl;

                if(msExperiment->getSRMSize() > 0)
                {
                    cout << "SRM count: " << msExperiment->getSRMSize() << endl;

                    Target target;
                    ostringstream oss;
                    for(size_t i=0; i < msExperiment->getSRMSize(); ++i)
                    {
                        msExperiment->getSRM(i, target);
                        cout << target.Q1 << "->" << target.Q3 <<
                               //" CE=" << target.collisionEnergy <<
                               //" DP=" << target.declusteringPotential <<
                               " DT=" << target.dwellTime << "; ";

                        double basePeakX, basePeakY;
                        msExperiment->getSIC(i, times, intensities, basePeakX, basePeakY);
                        if(!intensities.empty())
                            cout << "data summary: " << times.size() << " points, " <<
                                    "time range [" << times.front() << ", " << times.back() << "], " <<
                                    "base peak of " << basePeakY << " at " << basePeakX;
                        cout << endl;
                    }
                    //cout << oss.str() << endl;
                }

                for (int cycle=1; cycle <= endCycle; cycle += endCycle / 10)
                {
                    cout << "\nCycle " << cycle << endl;
                    SpectrumPtr spectrum = wiffFile->getSpectrum(msExperiment, cycle);
                    cout << "StartTime: " << spectrum->getStartTime() << endl;

                    if (spectrum->getDataSize(false) > 0)
                    {
                        cout << "DataSize: " << spectrum->getDataSize(false) << endl;
                        cout << "TIC: " << spectrum->getSumY() << endl;
                        cout << "Base Peak: " << spectrum->getBasePeakX() << " m/z (" << spectrum->getBasePeakY() << ")" << endl;
                        //cout << spectrum->getDataSize(true) << endl;
                        spectrum->getData(false, mz, intensities);
                        cout << "First and last points: " << mz.front() << ":" << intensities.front() << " - " << mz.back() << ":" << intensities.back() << endl;
                    }
                    else
                        cout << "No data." << endl;
                }
            }
        }
    }

    for (int sample=1; sample <= sampleCount; ++sample)
    {
        cout << "Sample " << sample << " (acquired " << wiffFile->getSampleAcquisitionTime(sample, true).to_string() << ")" << endl;

        typedef map<double, pair<ExperimentPtr, int> > ExperimentAndCycleByTime;
        ExperimentAndCycleByTime experimentAndCycleByTime;

        int periodCount = wiffFile->getPeriodCount(sample);
        for (int period=1; period <= periodCount; ++period)
        {
            int experimentCount = wiffFile->getExperimentCount(sample, period);
            for (int experiment=1; experiment <= experimentCount; ++experiment)
            {
                ExperimentPtr msExperiment = wiffFile->getExperiment(sample, period, experiment);

                vector<double> times, intensities;
                msExperiment->getTIC(times, intensities);
                for (int i=0; i < (int) times.size(); ++i)
                    experimentAndCycleByTime[times[i]] = make_pair(msExperiment, i+1);
            }
        }

        {
            bpt::ptime start = bpt::microsec_clock::local_time();
            BOOST_FOREACH(const ExperimentAndCycleByTime::value_type& itr, experimentAndCycleByTime)
                SpectrumPtr spectrum = wiffFile->getSpectrum(itr.second.first, itr.second.second);
            bpt::ptime stop = bpt::microsec_clock::local_time();
            cout << "Full metadata enumeration: " << bpt::to_simple_string(stop - start) << endl;
        }

        {
            vector<double> mz, intensities;
            bpt::ptime start = bpt::microsec_clock::local_time();
            BOOST_FOREACH(const ExperimentAndCycleByTime::value_type& itr, experimentAndCycleByTime)
            {
                SpectrumPtr spectrum = wiffFile->getSpectrum(itr.second.first, itr.second.second);
                spectrum->getData(false, mz, intensities);
            }
            bpt::ptime stop = bpt::microsec_clock::local_time();
            cout << "Full data enumeration: " << bpt::to_simple_string(stop - start) << endl;
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
