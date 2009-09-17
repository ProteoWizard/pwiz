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


#include "FeatureModeler.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <fstream>
#include <cstring>
#include <iterator>
#include "boost/filesystem/path.hpp"


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;
using boost::shared_ptr;
namespace bfs = boost::filesystem;


ostream* os_ = 0;


void testGaussian_Bombessin(Feature& bombessin2)
{
    if (os_) *os_ << "testGaussian_Bombessin()\n" << "before:\n" << bombessin2;

    FeatureModeler_Gaussian fm;
    Feature after = fm.fitFeature(bombessin2);

    if (os_) *os_ << "after:\n" << after;
}


void test(const bfs::path& datadir)
{
    string filename = (datadir / "Bombessin2.feature").string();
    ifstream is(filename.c_str());
    if (!is) throw runtime_error(("Unable to open file " + filename).c_str());

    Feature bombessin2;
    is >> bombessin2;
    unit_assert(bombessin2.peakels.size() == 5);

    testGaussian_Bombessin(bombessin2);
}


int main(int argc, char* argv[])
{
    try
    {
        bfs::path datadir = ".";

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datadir = bfs::path(argv[i]).branch_path(); 
        }   
        
        test(datadir);
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

