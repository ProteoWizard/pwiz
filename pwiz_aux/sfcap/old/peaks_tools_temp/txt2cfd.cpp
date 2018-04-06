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
#include <iostream>
#include <fstream>
#include <stdexcept>


using namespace std;
using namespace pwiz::data;


int main(int argc, char* argv[])
{
    if (argc != 3)
    {
        cout << "Usage: txt2cfd filename.txt filename.cfd\n";
        return 0;
    }

    try
    {
        const char* text = argv[1];
        const char* cfd = argv[2];

        cout << "Reading " << text << "..." << flush;
        const FrequencyData fd(text, FrequencyData::Text);
        cout << "done.\n";

        cout << "Writing " << cfd << "..." << flush;
        fd.write(cfd);
        cout << "done.\n";

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}
