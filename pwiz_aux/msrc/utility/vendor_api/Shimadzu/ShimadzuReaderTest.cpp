//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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


#include "ShimadzuReader.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::Shimadzu;


ostream* os_ = 0;


void test(const string& rawpath)
{
    cout << "Testing " << rawpath << endl;
    ShimadzuReaderPtr dataReader = ShimadzuReader::create(rawpath);

    vector<double> times, intensities;
    BOOST_FOREACH(const SRMTransition& transition, dataReader->getTransitions())
    {
        cout << "Transition Id=" << transition.id << " Q1=" << transition.Q1 << " Q3=" << transition.Q3 << " CE=" << transition.CE << " Polarity=" << transition.polarity << endl;
        ChromatogramPtr chromatogram = dataReader->getChromatogram(transition);
        chromatogram->getXArray(times);
        chromatogram->getYArray(intensities);
        if (!times.empty())
            cout << "Times (" << times.size() << ") [" << times.front() << "-" << times.back() << "]\n" << endl;
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
                                "\nUsage: ShimadzuReaderTest [-v] <source path 1> [source path 2] ..."); 

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
