//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "RawFile.h"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::vendor_api::Thermo;


int main(int argc, char* argv[])
{
    try
    {
        if (argc<2)
        {
            cout << "Usage: RawFileTest filename\n";
            return 1;
        }
        
        const char* filename = argv[1]; 

        RawFilePtr rawfile(RawFile::create(filename));
        rawfile->setCurrentController(Controller_MS, 1);

        cout << "name: " << rawfile->getFilename() << endl;
        cout << "scanCount: " << rawfile->getLastScanNumber() << endl;

        const MassRangePtr massRange = MassRangePtr(new MassRange());
        massRange->low = 400; massRange->high = 500;
        MassListPtr massListPtr = rawfile->getMassList(123, "", Cutoff_None, 0, 0, false);
        if (massListPtr->size() > 0)
            cout << "massList: " << massListPtr->mzArray.front() << "-" << massListPtr->mzArray.back() << endl;
        else
            cout << "massList empty" << endl;

        return 0;
    }
    catch (exception& e)
    {
        cout << "Caught exception: " << e.what() << endl;
    }
    catch (...)
    {
        cout << "Caught unknown exception.\n";
    }

    return 1;
}


