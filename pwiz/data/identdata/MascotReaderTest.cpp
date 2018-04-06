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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "MascotReader.hpp"
#include "IdentDataFile.hpp"
#include "DefaultReaderList.hpp"
#include "Diff.hpp"


using namespace pwiz::util;
using namespace pwiz::identdata;

ostream* os_ = 0;


void test(const string& datFile, const string& mzidFile)
{
#ifdef PWIZ_READER_MASCOT
    IdentDataFile expectedMzid(mzidFile);
    IdentDataFile testMzid(datFile);

    // remove ProteoWizard software and contact added when reading the expected mzIdentML
    expectedMzid.analysisSoftwareList.erase(expectedMzid.analysisSoftwareList.end() - 1);
    expectedMzid.auditCollection.erase(expectedMzid.auditCollection.end() - 1);

    Diff<IdentData, DiffConfig> diff(testMzid, expectedMzid);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);
#else
    unit_assert_operator_equal("Mascot DAT", DefaultReaderList().identify(datFile));
#endif
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        string datFile, mzidFile;

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else if (bal::iends_with(argv[i], ".mzid") || bal::iends_with(argv[i], ".mzid.gz"))
                mzidFile = argv[i];
            else if (bal::iends_with(argv[i], ".dat") || bal::iends_with(argv[i], ".dat"))
                datFile = argv[i];
            else if (!bal::starts_with(argv[i], "-"))
                throw runtime_error(string("no support for extra argument \"") + argv[i] + "\"");
        }

        test(datFile, mzidFile);
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
