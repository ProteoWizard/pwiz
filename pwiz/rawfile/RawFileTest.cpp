//
// RawFileTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


using namespace std;
using namespace pwiz::raw;


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

        RawFileLibrary rawFileLibrary;

        RawFilePtr rawfile(filename);
        rawfile->setCurrentController(Controller_MS, 1);

        cout << "name: " << rawfile->value(FileName) << endl;
        cout << "scanCount: " << rawfile->value(NumSpectra) << endl;

        return 0;
    }
    catch (RawEgg& egg)
    {
        cout << "Caught RawEgg: " << egg.error() << endl;
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


