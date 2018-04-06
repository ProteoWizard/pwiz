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
    if (argc < 4)
    {
        cout << "Usage: tlperror model.tlp data.cfd radius\n";
        return 1;
    }

    const char* filenameModel = argv[1];
    const char* filenameData = argv[2];
    int radius = atoi(argv[3]);
   
    try
    {
        if (radius < 1)
            throw runtime_error("Positive radius required.\n");

        TruncatedLorentzianParameters tlp(filenameModel);
        const FrequencyData fdOriginal(filenameData);
        auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create();

        const FrequencyData fd(fdOriginal, fdOriginal.max(), radius);
        
        double N = fd.data().size();
        double error = estimator->error(fd, tlp);
        double normalizedError = estimator->normalizedError(fd, tlp);
        double noiseVariance = fd.noiseFloor() * fd.noiseFloor();
        double zscore = (error/noiseVariance - N) / (sqrt(N)); 
        double sumSquaresModel = estimator->sumSquaresModel(fd, tlp);

        cout << "# N error normalizedError noiseVariance z-score sumSquaresModel\n";

        cout << N << " "; 
        cout << error << " ";
        cout << normalizedError << " ";
        cout << noiseVariance << " ";
        cout << zscore << " ";
        cout << sumSquaresModel << " ";
        cout << endl;

        return 0; 
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

