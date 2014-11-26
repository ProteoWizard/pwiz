//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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


#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>
#include <fstream>


using namespace std;
using namespace pwiz::cv;
using namespace pwiz::identdata;
using namespace pwiz::proteome;


int dofile(const string& filename)
{
    IdentDataFile idf(filename, 0, 0, true);

    string spectraDataName = idf.dataCollection.inputs.spectraData[0]->name;
    if (spectraDataName.empty())
    {
        const string& location = idf.dataCollection.inputs.spectraData[0]->location;
        spectraDataName = bfs::change_extension(bfs::path(location), "").filename();

        if (spectraDataName.empty())
            throw runtime_error("no spectrum source name or location");
    }
    cout << spectraDataName << endl;

    return 0;
}


int main(int argc, const char* argv[])
{
    try
    {
        if (argc != 2)
            throw runtime_error("Usage: iddump filename\n");
    
        const char* filename = argv[1];

        return dofile(filename);
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[msdiff] Caught unknown exception.\n";
    }

    return 1;
}

