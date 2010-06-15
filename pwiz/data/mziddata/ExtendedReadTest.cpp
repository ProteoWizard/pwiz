//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "MzIdentMLFile.hpp"
#include "DefaultReaderList.hpp"
#include "IO.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::mziddata;

const char* filenames[] =
{
    "Mascot_MSMS_example.mzid",
    "omssa_example_full.mzid",
    "Mascot_mzml_example.mzid",
    "PMF_example.mzid",
    "Mascot_N15_example.mzid",
    "Sequest_example.mzid",
    "Mascot_NA_example.mzid",
    "spectraST.mzid",
    "Mascot_top_down_example.mzid",
    "xtandem_example_full.mzid",
    "MPC_example.mzid"
};

void testFile(const string& inFilepath, const string& outFilepath)
{
    cout << "reading file in from " << inFilepath << endl;
    MzIdentMLFile mzid(inFilepath);

    cout << "writing file out to " << outFilepath << endl;
    mzid.write(outFilepath);
    cout << "done.\n";
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc == 3)
            testFile(argv[1], argv[2]);
        else
        {
            cout << "only have " << argc << " arguments:\n";
            for(int i=0; i<argc; i++)
                cout << argv[i] << endl;
        }

        cout << "\nhttp://proteowizard.sourceforge.net\n"
             << "support@proteowizard.org\n";

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
