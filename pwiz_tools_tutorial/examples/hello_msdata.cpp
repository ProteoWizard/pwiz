//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;


void test()
{
    cout << "Hello, MSData!\n";

    // create the MSData object in memory
    MSData msd;
    examples::initializeTiny(msd); 

    // write MSData object to a stream
    ostringstream oss;
    Serializer_mzML serializer;
    serializer.write(oss, msd);

    // read back into another object
    MSData msd2;
    shared_ptr<istream> iss(new istringstream(oss.str()));
    serializer.read(iss, msd2);

    // do a diff on the two objects
    Diff<MSData, pwiz::msdata::DiffConfig> diff(msd, msd2); 
    unit_assert(!diff);

    // write out mzML 
    string filename = "tiny.pwiz.mzML";
    cout << "Writing file " << filename << endl;
    MSDataFile::write(msd, filename);

    // write out mzXML
    filename = "tiny.pwiz.mzXML";
    cout << "Writing file " << filename << endl;
    MSDataFile::write(msd, filename, MSDataFile::Format_mzXML);
}


int main()
{
    try
    {
        test();

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

