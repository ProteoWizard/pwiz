//
// $Id$ 
//
// Original author: Nathan Edwards <nje5@georgetown.edu>
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

#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::identdata;

#include "TextPSMReader.hpp"

using namespace pwiz::examples;

int main()
{
    try
    {
	IdentData mzid;
        TextPSMReader reader;

	reader.readTextStream(std::cin, mzid);

	IdentDataFile::write(mzid, "", std::cout);

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
