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

#include "diff_std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <iostream>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::diff_std;

ostream* os_ = 0;

void testString(const string& a, const string& b)
{
    if (os_) *os_ << "diff_string(\"" << a << "\", \"" << b << "\")" << endl;

    string a_b, b_a;
    diff_string(a, b, a_b, b_a);
    if (os_) *os_ << "a-b: " << a_b << "\nb-a: " << b_a << endl;

    if (a == b)
        unit_assert(a_b.empty() && b_a.empty());
    else
        unit_assert(!a_b.empty() && !b_a.empty());
}

template <typename integral_type>
void testIntegralReally(integral_type a, integral_type b)
{
    if (os_) *os_ << "diff_integral(\"" << a << "\", \"" << b << "\")" << endl;

    integral_type a_b, b_a;
    diff_integral(a, b, a_b, b_a);
    if (a == b)
        unit_assert(a_b == integral_type() && b_a == integral_type());
    else
        unit_assert(a_b != integral_type() || b_a != integral_type());
}

template <typename integral_type>
void testIntegral()
{
    testIntegralReally<int>(1, 1);
    testIntegralReally<int>(-1, 1);
    testIntegralReally<int>(-1, -1);
    testIntegralReally<int>(1, 0);
    testIntegralReally<int>(-1, 0);
}

template <typename floating_type>
void testFloating(floating_type a, floating_type b, floating_type precision)
{
    floating_type a_b, b_a;

    diff_floating(a, b, a_b, b_a, precision);
    if (fabs(a - b) <= precision + std::numeric_limits<floating_type>::epsilon())
        unit_assert(a_b == floating_type() && b_a == floating_type());
    else
        unit_assert(a_b == fabs(a - b) && b_a == fabs(a - b));
}

void test()
{
    testString("goober", "goober");
    testString("goober", "goo");

    testIntegral<int>();
    testIntegral<short>();
    testIntegral<long>();
    testIntegral<unsigned int>();
    testIntegral<unsigned short>();
    testIntegral<unsigned long>();

    testFloating<float>(1.f, 1.f, 1.e-6f);
    testFloating<float>(1.f, 1.0000000001f, 1.e-6f);
    testFloating<float>(1.f, 1.00001f, 1.e-6f);
    testFloating<float>(4.f, 4.2f, 1.f);

    testFloating<double>(1, 1, 1e-6);
    testFloating<double>(1, 1.0000000001, 1e-6);
    testFloating<double>(1, 1.00001, 1e-6);
    testFloating<double>(4, 4.2, 1);
}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

