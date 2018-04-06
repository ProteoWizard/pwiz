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


#include "data/TransientData.hpp"
#include "data/FrequencyData.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::data;


int main(int argc, char* argv[])
{
    if (argc < 3)
    {
        cout << "Usage: midas2cfd filename.dat filename.cfd [zeropad=1]\n";
        return 0;
    }

    try
    {
        const char* filenameIn = argv[1];
        const char* filenameOut = argv[2];
        int zeroPadding = argc>3 ? atoi(argv[3]) : 1;

        TransientData td(filenameIn);
        FrequencyData fd;
        td.computeFFT(zeroPadding, fd);
        fd.write(filenameOut);

        cout << "ok\n";
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
    }
}



