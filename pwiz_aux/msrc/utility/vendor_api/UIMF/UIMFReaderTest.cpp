//
// $Id$
//
//
// Original author: Brendan MacLean <brendanx .@. u.washington.edu>
//
// Copyright 2009 University of Washington - Seattle, WA
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


#include "UIMFReader.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::UIMF;


ostream* os_ = 0;


void test(const string& rawpath)
{
    cout << "Testing " << rawpath << endl;
    UIMFReaderPtr dataReader = UIMFReader::create(rawpath);
    cout << dataReader->getAcquisitionTime().to_string() << endl;

    if (dataReader->getIndex().empty())
        throw runtime_error("[UIMFReaderTest] No spectra detected in test data at: " + rawpath);

    vector<double> mz;
    vector<double> intensities;
    for (int i = 0; i < 10 && i < dataReader->getIndex().size(); ++i)
    {
        const UIMFReader::IndexEntry& ie0 = dataReader->getIndex()[i];
        dataReader->getScan(ie0.frame, ie0.scan, ie0.frameType, mz, intensities);
        cout << "Drift time: " << dataReader->getDriftTime(ie0.frame, ie0.scan) << "; retention time: " << dataReader->getRetentionTime(ie0.frame) << endl;
        if (!mz.empty())
            cout << "Masses (" << mz.size() << ") [" << mz.front() << "-" << mz.back() << "]" << " Intensities [" << intensities.front() << "-" << intensities.back() << "]" << endl;
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
                                "\nUsage: UIMFReaderTest [-v] <source path 1> [source path 2] ..."); 

        for (size_t i=0; i < rawpaths.size(); ++i)
            for (bfs::directory_iterator itr(rawpaths[i]); itr != bfs::directory_iterator(); ++itr)
            {
                if (!bal::iequals(itr->path().extension().string(), ".uimf"))
                    continue;
                test(itr->path().string());
            }
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
