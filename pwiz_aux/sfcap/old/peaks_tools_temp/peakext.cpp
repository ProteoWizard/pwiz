//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "data/FrequencyData.hpp"
#include "peaks/PeakInfo.hpp"
#include "peaks/PeakDetectorNaive.hpp"
#include "extstd/auto_vector.h"
#include <iostream>
#include <iterator>
#include <stdexcept>
#include <fstream>
#include <iomanip>
#include <algorithm>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::data;


void createOutputDirectory(const string& path)
{
    string systemCommand = "mkdir " + path + " 2> nul";
    system(systemCommand.c_str());
}


string outputFilename(const string& outputDirectory, int index, int digits)
{
    ostringstream result;
    result << outputDirectory << "/peak." <<
        setw(digits) << setfill('0') << index << ".cfd";
    return result.str();
}


int digitCount(int n)
{
    int result = 0;
    while (n>0)
    {
        n/=10;
        result++;
    }
    return result;
}


double height(const PeakInfo& pi)
{
    return abs(pi.intensity);
}


void extractPeaks(const char* filename, 
                  const char* outputDirectory, 
                  double noiseFactor, 
                  int windowRadius, 
                  int detectionRadius)
{
    cout.precision(10);

    cout << "filename: " << filename << endl;
    cout << "outputDirectory: " << outputDirectory << endl;
    cout << "noiseFactor: " << noiseFactor << endl;
    cout << "windowRadius: " << windowRadius << endl;
    cout << "detectionRadius: " << detectionRadius << endl;

    cout << "Reading data..." << flush;
    FrequencyData fd(filename);
    cout << "done.\n";

    createOutputDirectory(outputDirectory);

    // find peaks
    auto_ptr<PeakDetectorNaive> pd = PeakDetectorNaive::create(noiseFactor, detectionRadius);
    vector<PeakInfo> peaks;
    pd->findPeaks(fd, peaks);
    cout << "Peaks found: " << peaks.size() << endl;

    // calculate min height to report, based on max peak count 

    const unsigned int maxPeakCount = 200;
    double minHeight = 0;

    if (peaks.size() > maxPeakCount)
    {
        vector<double> heights;
        transform(peaks.begin(), peaks.end(), back_inserter(heights), height); 
        sort(heights.begin(), heights.end(), greater<double>()); 
        minHeight = heights[maxPeakCount] + 1e-6; 
    }
    
    cout << "Reporting peaks with magnitude > " << minHeight << endl;
   
    // report the peaks

    int digits = digitCount(peaks.size());

    string freqz_filename = "/freqz";
    freqz_filename = outputDirectory + freqz_filename;
    ofstream freqz(freqz_filename.c_str());
    if (!freqz) throw runtime_error("Unable to open freqz.");
    freqz.precision(10);

    const double noiseLevel = sqrt(fd.variance());

    vector<PeakInfo>::const_iterator it=peaks.begin();
    for (int i=0; it!=peaks.end(); ++it, ++i)
    {
        if (height(*it) > minHeight)
        {
            FrequencyData::const_iterator fdit = fd.findNearest(it->frequency);
            auto_ptr<FrequencyData> window(new FrequencyData(fd, fdit, windowRadius));
            window->noiseFloor(noiseLevel);
            window->write(outputFilename(outputDirectory, i, digits));

            freqz << it->frequency << " 1\n";
        }
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 3)
        {
            cout << "Usage: peakext filename outputDirectory"
                << " [noiseFactor=5] [windowRadius=10] [detectionRadius=2]\n";
            cout << "Reports peaks with magnitude > noise*noiseFactor.\n";
            return 0;
        }

        cout << "peakext\n";

        const char* filename = argv[1];
        const char* outputDirectory = argv[2];
        double noiseFactor = argc>3 ? atof(argv[3]) : 5;
        int windowRadius = argc>4 ? atoi(argv[4]) : 10;
        int detectionRadius = argc>5 ? atoi(argv[5]) : 2;

        extractPeaks(filename, outputDirectory, noiseFactor, windowRadius, detectionRadius);

        return 0;
    }
    catch (exception& e)
    {
        cerr << "Caught exception: " << e.what() << endl;
        return 1;
    }
}

