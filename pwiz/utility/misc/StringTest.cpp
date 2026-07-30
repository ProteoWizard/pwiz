//
// $Id$
//
//
// Original author: Brendan MacLean <brendanx .at. uw.edu>
//
// Copyright 2026 University of Washington - Seattle, WA
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


#include "Std.hpp"
#include "String.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <cstdlib>
#include <limits>


using namespace pwiz::util;


ostream* os_ = 0;


// The property that matters: whatever toString writes, reading it back must
// give the identical double. Without this, a value can be serialized to mzML
// and reloaded a ULP away, and nothing in the pipeline notices.
void testRoundTrip()
{
    if (os_) *os_ << "testRoundTrip()\n";

    const double values[] =
    {
        // Retention times observed in a Thermo DIA acquisition. These are the
        // motivating case: the 12-fractional-digit fast path truncated them,
        // so an mzML written from a .raw could not reproduce the raw value.
        1.8119944333330003,
        3.0261190166670002,
        0.5903116999999999,
        4.774856783332999,
        // Ordinary values that the fast path already represents exactly; these
        // must keep their existing short text (see testFastPathTextUnchanged).
        0.0, 1.0, 0.1, 12.5, 500.25, -3.75,
        // Range extremes and awkward magnitudes. DBL_MAX is the one that caught
        // toString accepting a 15-digit form whose decimal exceeds DBL_MAX: an
        // out-of-range istringstream extraction stores the largest representable
        // value, so the round-trip check saw equality where strtod sees inf.
        1e-9, 1e9, 1.7976931348623157e308, 2.2250738585072014e-308,
        std::numeric_limits<double>::epsilon(),
        // A value whose shortest round-trip form needs all 17 digits.
        0.10000000000000002,
    };

    for (size_t i = 0; i < sizeof(values) / sizeof(values[0]); ++i)
    {
        double value = values[i];
        string text = toString(value);
        // strtod, not the istringstream toString validates with: the oracle has
        // to be a reader independent of the code under test, and the two differ
        // on out-of-range text, which is where the defect was.
        double reloaded = strtod(text.c_str(), NULL);
        if (os_) *os_ << "  " << text << " <- " << value << endl;
        unit_assert_operator_equal(value, reloaded);
    }
}


// Every double that survives a serialize/parse cycle must do so for the RIGHT
// reason. Walking a wide range of magnitudes catches a policy change that
// happens to work for the handful of literals above.
void testRoundTripAcrossMagnitudes()
{
    if (os_) *os_ << "testRoundTripAcrossMagnitudes()\n";

    // A cheap deterministic generator; no <random> so the sequence is identical
    // on every platform and a failure is reproducible from the seed alone.
    unsigned int seed = 20260729u;
    for (int i = 0; i < 20000; ++i)
    {
        seed = seed * 1103515245u + 12345u;
        double mantissa = (double)(seed % 1000000007u) / 1000000007.0;
        seed = seed * 1103515245u + 12345u;
        int exponent = (int)(seed % 40u) - 20;   // 1e-20 .. 1e19
        double value = mantissa * pow(10.0, exponent);

        string text = toString(value);
        unit_assert_operator_equal(value, strtod(text.c_str(), NULL));
    }
}


// The round-trip guarantee must not come at the cost of noisy output: values
// the 12-digit path already renders exactly keep exactly that text, so existing
// mzML output and file sizes are unchanged.
void testFastPathTextUnchanged()
{
    if (os_) *os_ << "testFastPathTextUnchanged()\n";

    unit_assert_operator_equal(string("0.0"), toString(0.0));
    unit_assert_operator_equal(string("1.0"), toString(1.0));
    unit_assert_operator_equal(string("0.1"), toString(0.1));
    unit_assert_operator_equal(string("12.5"), toString(12.5));
    unit_assert_operator_equal(string("500.25"), toString(500.25));
    unit_assert_operator_equal(string("-3.75"), toString(-3.75));
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testRoundTrip();
        testRoundTripAcrossMagnitudes();
        testFastPathTextUnchanged();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }

    TEST_EPILOG
}
