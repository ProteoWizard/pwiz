///
/// MatrixTest.cpp
///

#include "Matrix.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <limits>

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

const double epsilon = 2 * numeric_limits<double>::epsilon();

void testMatrix()
{
    // initialize a Matrix
    Matrix m(2,4);

    // insert some stuff
    m.insert(3.141, 0,0);
    m.insert(2.718, 1,3);

    // access some stuff
    unit_assert_equal(m.access(0,0), 3.141, epsilon);
    unit_assert_equal(m.access(1,3), 2.718, epsilon);

}

int main()
{
    testMatrix();
    return 0;

}

