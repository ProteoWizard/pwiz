///
/// DatabaseQueryTest.cpp
///

#include "DatabaseQuery.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

void test()
{
    double mu1 = 1;
    double mu2 = 2;
    double sigma1 = 2;
    double sigma2 = 4;
    double threshold = 0.7;
    
    DatabaseQuery dbQuery(PidfPtr(new PeptideID_dataFetcher()));

    // Given a normal distribution fit to mz and rt differences, calculate the folded normal distribution correspoding to the parameters of the original distribution.  Using this distribution and an explicit approximation to the error function, calculate the region of mz x rt space that it is necessary to search in order to find all the matches that would score higher than the given threshold.

    pair<double,double> radii = dbQuery.calculateSearchRegion(mu1, mu2, sigma1, sigma2, threshold);    
    unit_assert_equal(radii.first, 10.047046209584696, .000001);

}

int main()
{
    test();
    return 0;
}
