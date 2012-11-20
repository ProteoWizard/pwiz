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
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::data::diff_impl;

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
    diff_integral(a, b, a_b, b_a, BaseDiffConfig());
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
    BaseDiffConfig config((double) precision);

    diff_floating(a, b, a_b, b_a, config);
    if (fabs(a - b) <= config.precision + std::numeric_limits<floating_type>::epsilon())
        unit_assert(a_b == floating_type() && b_a == floating_type());
    else
        unit_assert(a_b == fabs(a - b) && b_a == fabs(a - b));
}


void testCV()
{
    if (os_) *os_ << "testCV()\n";

    CV a, b;
    a.URI = "uri";
    a.id = "cvLabel";
    a.fullName = "fullName";
    a.version = "version";
    b = a;

    Diff<CV> diff;
    diff(a,b);

    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());
    unit_assert(!diff);

    a.version = "version_changed";

    diff(a,b); 
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.URI.empty() && diff.b_a.URI.empty());
    unit_assert(diff.a_b.id.empty() && diff.b_a.id.empty());
    unit_assert(diff.a_b.fullName.empty() && diff.b_a.fullName.empty());
    unit_assert(diff.a_b.version == "version_changed");
    unit_assert(diff.b_a.version == "version");
}


void testUserParam()
{
    if (os_) *os_ << "testUserParam()\n";

    UserParam a, b;
    a.name = "name";
    a.value = "value";
    a.type = "type";
    a.units = UO_minute;
    b = a;

    Diff<UserParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    a.units = UO_second;
    unit_assert(diff(a,b));
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.name == "name");
    unit_assert(diff.b_a.name == "name");
    unit_assert(diff.a_b.value == "value");
    unit_assert(diff.b_a.value == "value_changed");
    unit_assert(diff.a_b.type.empty() && diff.b_a.type.empty());
    unit_assert(diff.a_b.units == UO_second);
    unit_assert(diff.b_a.units == UO_minute);
}


void testCVParam()
{
    if (os_) *os_ << "testCVParam()\n";

    CVParam a, b, c;
    a.cvid = MS_ionization_type; 
    a.value = "420";
    c = b = a;

    Diff<CVParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    diff(a,b);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.cvid == MS_ionization_type);
    unit_assert(diff.b_a.cvid == MS_ionization_type);
    unit_assert(diff.a_b.value == "420");
    unit_assert(diff.b_a.value == "value_changed");

    c.value = "421"; // prove fix for bug that wouldn't catch diff in int values
    diff(a,c);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.cvid == MS_ionization_type);
    unit_assert(diff.b_a.cvid == MS_ionization_type);
    unit_assert(diff.a_b.value == "420");
    unit_assert(diff.b_a.value == "421");

    a.value = "4.1e5"; // make sure we handle scientific notation properly
    c.value = "4.1"; 
    diff(a,c);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;

    a.value = "4.1e5"; // make sure we handle scientific notation properly
    c.value = "410000.0"; 
    diff(a,c);
    unit_assert(!diff);
    if (os_) *os_ << diff << endl;

    a.value = "1a"; // make sure we aren't naive about things that start out as ints
    c.value = "1b"; 
    diff(a,c);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;


}


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n";

    ParamGroupPtr pgp1(new ParamGroup("pg1"));
    ParamGroupPtr pgp2(new ParamGroup("pg2"));
    ParamGroupPtr pgp3(new ParamGroup("pg3"));
 
    ParamContainer a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.paramGroupPtrs.push_back(pgp1);
    b.paramGroupPtrs.push_back(pgp1);
   
    Diff<ParamContainer> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));
    a.cvParams.push_back(MS_charge_state);
    b.cvParams.push_back(MS_peak_intensity);
    a.paramGroupPtrs.push_back(pgp2);
    b.paramGroupPtrs.push_back(pgp3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));

    unit_assert(diff.a_b.cvParams.size() == 1);
    unit_assert(diff.a_b.cvParams[0] == MS_charge_state); 
    unit_assert(diff.b_a.cvParams.size() == 1);
    unit_assert(diff.b_a.cvParams[0] == MS_peak_intensity); 

    unit_assert(diff.a_b.paramGroupPtrs.size() == 1);
    unit_assert(diff.a_b.paramGroupPtrs[0]->id == "pg2"); 
    unit_assert(diff.b_a.paramGroupPtrs.size() == 1);
    unit_assert(diff.b_a.paramGroupPtrs[0]->id == "pg3"); 
}


void testParamGroup()
{
    if (os_) *os_ << "testParamGroup()\n";

    ParamGroup a("pg"), b("pg");
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<ParamGroup> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));
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

    testCV();
    testUserParam();
    testCVParam();
    testParamContainer();
    testParamGroup();
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

