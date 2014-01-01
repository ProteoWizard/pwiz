//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "sld.hpp"
#include <iostream>
#include <stdexcept>


using namespace std;


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 3) throw runtime_error("Usage: sldout [csv|txt] filename.sld");
        const string& outputType = argv[1];
        const string& filename = argv[2];

        const pwiz::sld::File file(filename);

        if (outputType == "csv")
            file.writeCSV(cout);
        else if (outputType == "txt")
            file.writeText(cout);
        else
            throw runtime_error("Output type must be \"csv\" or \"txt\"."); 

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
    }
    catch (...)
    {
        cout << "Unknown exception.\n"; 
    }
    
    return 1;
}

