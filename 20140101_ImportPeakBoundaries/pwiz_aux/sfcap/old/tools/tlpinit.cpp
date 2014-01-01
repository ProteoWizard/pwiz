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
#include <iomanip>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::data;


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 3)
        {
            cout << "Usage: tlpinit filename.cfd filename.tlp\n";
            return 1; 
        }

        const char* inputFilename = argv[1];
        const char* outputFilename = argv[2];

        cout << "Reading data from " << inputFilename << endl;
        FrequencyData fd(inputFilename);

        auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create(); 
        TruncatedLorentzianParameters tlp = estimator->initialEstimate(fd);

        cout << tlp << endl;
        cout << "Writing output to " << outputFilename << endl;
        tlp.write(outputFilename);

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

