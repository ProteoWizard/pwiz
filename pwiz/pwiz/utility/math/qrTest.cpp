//
// $Id$
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

#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/io.hpp>

#include "pwiz/utility/misc/Std.hpp"
//#include <execinfo.h>

#include "pwiz/utility/misc/unit.hpp"
#include "Types.hpp"
#include "qr.hpp"

using namespace boost::numeric::ublas;
using namespace pwiz::math;
using namespace pwiz::math::types;
using namespace pwiz::util;


ostream* os_ = 0;
const double epsilon = 1e-12;


template<class matrix_type>
bool isUpperTriangular(const matrix_type& A, double eps)
{
    typedef typename matrix_type::size_type size_type;
    bool upper = true;
    
    for (size_type i=1; i<A.size2() && upper; i++)
    {
        for (size_type j=0; j<i && upper; j++)
        {
            upper = fabs(A(i,j)) < eps;
        }
    }

    return upper;
}


void testReflector()
{
    if (os_) *os_ << "testReflector() begin" << endl;

    dmatrix F(3,3);
    dvector x(3);

    x(0) = 1;
    x(1) = 1;
    x(2) = 0;

    Reflector(x, F);

    dvector v = prod(F, x);

    unit_assert_equal(fabs(v(0)), norm_2(x), epsilon);
    unit_assert_equal(v(1), 0, epsilon);
    unit_assert_equal(v(2), 0, epsilon);
    
    x(0) = -1;
    x(1) = 1;
    x(2) = 0;

    if (os_) *os_ << "testReflector() end" << endl;
}

void testQR()
{
    if (os_) *os_ << "testQR() begin" << endl;
    
    dmatrix A(3,3);
    dmatrix Q(3,3);
    dmatrix R(3,3);

    for (dmatrix::size_type i=0; i< A.size1(); i++)
    {
        for (dmatrix::size_type j=0; j<A.size2(); j++)
        {
            A(i,j) = ((i==j) || (j == 0));
        }
    }

    try {
        qr<dmatrix>(A, Q, R);

        unit_assert(isUpperTriangular(R, epsilon));

        dmatrix diff = (A - prod(Q, R));

        unit_assert_equal(norm_1(diff), 0, epsilon);

        identity_matrix<dmatrix::value_type> eye(Q.size1());
        diff = prod(Q, herm(Q)) - eye;
        
        unit_assert_equal(norm_1(diff), 0, epsilon);        
    }
    catch (boost::numeric::ublas::bad_argument ba)
    {
        if (os_) *os_ << "exception: " << ba.what() << endl;
    }
    
    if (os_) *os_ << "testQR() end" << endl;
}

void testRectangularQR()
{
    if (os_) *os_ << "testRectangularQR() begin" << endl;
    
    dmatrix A(5,3);
    dmatrix Q;
    dmatrix R;

    for (dmatrix::size_type i=0; i< A.size1(); i++)
    {
        for (dmatrix::size_type j=0; j<A.size2(); j++)
        {
            A(i,j) = ((i==j) || (j == 0));
        }
    }

    try {
        qr<dmatrix>(A, Q, R);

        unit_assert(isUpperTriangular(R, epsilon));

        dmatrix diff = (A - prod(Q, R));

        unit_assert_equal(norm_1(diff), 0, epsilon);

        identity_matrix<dmatrix::value_type> eye(Q.size1());
        diff = prod(Q, herm(Q)) - eye;
        
        unit_assert_equal(norm_1(diff), 0, epsilon);        
    }
    catch (boost::numeric::ublas::bad_argument ba)
    {
        if (os_) *os_ << "exception: " << ba.what() << endl;
    }
    
    if (os_) *os_ << "testRectangularQR() end" << endl;
}

int main(int argc, char** argv)
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
    if (os_) *os_ << "qrTest\n";
    testReflector();
    testQR();
    testRectangularQR();

    return 0;
}
