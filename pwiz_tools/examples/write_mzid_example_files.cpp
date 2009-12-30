//
// $Id$
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
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

#define PWIZ_SOURCE

#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/mziddata/examples.hpp"
#include "pwiz/data/mziddata/DefaultReaderList.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::data;
using namespace pwiz::mziddata;
using boost::shared_ptr;



void writeTiny()
{
    MzIdentML mzid;
    examples::initializeTiny(mzid);
    

    // write out traML 
    string filename = "tiny.pwiz.mzid";
    cout << "Writing file " << filename << endl;
    // call after writer creation
    MzIdentMLFile::write(mzid, filename);
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
