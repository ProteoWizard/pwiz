///
/// SearchNeighborhoodCalculatorTest.cpp
///

#include "SearchNeighborhoodCalculator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

using namespace pwiz::eharmony;
using namespace pwiz::util;

ostream* os_ = 0;

void test()
{
    if (os_) *os_ << "test() ..." << endl;

    SearchNeighborhoodCalculator snc;

    SpectrumQuery sq;
    sq.precursorNeutralMass = 1;
    sq.assumedCharge = 2;
    sq.retentionTimeSec = 40;

    Feature f;
    f.mzMonoisotopic = 1.510;
    f.retentionTime = 98;

    unit_assert(snc.close(sq,f));
    if (os_)
        {
            XMLWriter writer(*os_);
            sq.write(writer);
            f.write(writer);

        }
}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "SearchNeighborhoodCalculatorTest: " << endl;
            test();

        }

    catch (std::exception& e)
        {
            cerr << e.what() << endl;
            return 1;

        }

    catch (...)
        {
            cerr << "Caught unknown exception.\n";
            return 1;

        }

}

