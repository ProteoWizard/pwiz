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


#include "pwiz/data/tradata/TraDataFile.hpp"
#include "pwiz/data/tradata/examples.hpp"
#include "pwiz/data/tradata/DefaultReaderList.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::data;
using namespace pwiz::tradata;


void writeTiny()
{
    // create the TraData object in memory
    TraData td;
    examples::initializeTiny(td); 

    // write out traML 
    string filename = "tiny.pwiz.traML";
    cout << "Writing file " << filename << endl;
    TraDataFile::write(td, filename);

    // with MIAPE metadata added 
    examples::addMIAPEExampleMetadata(td);
    filename = "tiny_miape.pwiz.traML";
    cout << "Writing file " << filename << endl;
    TraDataFile::write(td, filename);
}


int main()
{
    try
    {
        writeTiny();

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
