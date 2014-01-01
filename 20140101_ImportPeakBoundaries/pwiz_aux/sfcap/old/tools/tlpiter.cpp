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


#include "peaks/TruncatedLorentzianEstimator.hpp"
#include <iostream>
#include <fstream>
#include <vector>
#include <sstream>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::data;


int main(int argc, char* argv[])
{
    if (argc < 5)
    {
        cout << "Usage: tlpiter filename.cfd initial.tlp final.tlp iterationCount [options]\n";
        cout << "Options:\n";
        cout << "    -output=pathname (write intermediate files in pathname)\n";
        return 1;
    }

    const char* filenameData = argv[1];
    const char* filenameInitial = argv[2];
    const char* filenameFinal = argv[3];
    int iterationCount = atoi(argv[4]);
    string outputDirectory;
    
    for (int i=5; i<argc; i++)
    {
        string option = argv[i];

        if (option.substr(0,8) == "-output=") 
        {
            outputDirectory = option.substr(8); 
            if (!system(("mkdir " + outputDirectory).c_str()))
                cout << "Created directory " << outputDirectory << endl;
            cout << "Writing intermediate files to " << outputDirectory << endl; 
        } 
        else
            cout << "Ignoring unknown option '" << option << "'\n";
    }

    try
    {
        FrequencyData fd(filenameData);
        TruncatedLorentzianParameters tlp(filenameInitial);
        
        auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create();
        estimator->outputDirectory(outputDirectory);
        tlp = estimator->iteratedEstimate(fd, tlp, iterationCount);

        cout << "Writing " << filenameFinal << endl;
        tlp.write(filenameFinal);

        return 0; 
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

