//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

