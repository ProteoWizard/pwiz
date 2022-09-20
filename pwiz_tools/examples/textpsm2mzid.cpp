//
// Original author: Nathan Edwards <nje5@georgetown.edu>
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
