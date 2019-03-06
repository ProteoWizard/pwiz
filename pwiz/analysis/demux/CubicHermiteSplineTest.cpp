//
// $Id$
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/analysis/demux/CubicHermiteSpline.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;

class CubicHermiteSplineTest {
public:
    void Run()
    {
        SetUp();
        InterpolationTest();
        ErrorTest();
        TearDown();
    }

protected:

    virtual void SetUp()
    {
    }

    void TearDown()
    {
    }

    void InterpolationTest()
    {
        vector<double> x;
        vector<double> y;
        x.push_back(3.1415);
        x.push_back(3.63753);
        x.push_back(4.13355);
        x.push_back(4.62958);
        x.push_back(5.12561);
        x.push_back(5.62163);
        x.push_back(6.11766);
        x.push_back(6.61368);
        x.push_back(7.10971);
        x.push_back(7.60574);
        x.push_back(8.10176);
        x.push_back(8.59779);
        x.push_back(9.09382);
        x.push_back(9.58984);
        x.push_back(10.0859);
        x.push_back(10.5819);
        x.push_back(11.0779);
        x.push_back(11.5739);
        x.push_back(12.07);
        x.push_back(12.566);
        y.push_back(-5.89868e-005);
        y.push_back(0.233061);
        y.push_back(0.216427);
        y.push_back(0.0486148);
        y.push_back(-0.133157);
        y.push_back(-0.172031);
        y.push_back(-0.0456079);
        y.push_back(0.0906686);
        y.push_back(0.116462);
        y.push_back(0.0557287);
        y.push_back(-0.03875);
        y.push_back(-0.10346);
        y.push_back(-0.0734111);
        y.push_back(0.0298435);
        y.push_back(0.094886);
        y.push_back(0.0588743);
        y.push_back(-0.0171021);
        y.push_back(-0.0630512);
        y.push_back(-0.0601684);
        y.push_back(-0.00994154);

        vector<double> xIn;
        vector<double> yIn;
        const double PI = 3.1415;
        const int N = 12;
        double xx = PI;
        double step = 4 * PI / (N - 1);
        for (int i = 0; i < N; ++i, xx += step)
        {
            xIn.push_back(xx);
            yIn.push_back(sin(2 * xx) / xx);
        }

        CubicHermiteSpline interpolator(xIn, yIn);
        const int N_out = 20;
        xx = PI;
        step = (3 * PI) / (N_out - 1);
        for (int i = 0; i < N_out; ++i, xx += step)
        {
            double interpolatedX = interpolator.Interpolate(xx);
            double diff = abs(interpolatedX - y[i]);
            unit_assert(diff < 1.0);
        }
    }

    void CubicSplineDummyInitialize(const vector<double>& x, const vector<double>& y)
    {
        CubicHermiteSpline interpolator(x, y);
    }

    void ErrorTest()
    {
        // Should throw on empty data
        vector<double> x;
        vector<double> y;
        unit_assert_throws_what(CubicSplineDummyInitialize(x, y), std::runtime_error, "[CubicHermiteSpline] unusable values were provided to the spline function.");

        // Should throw on unsorted x data
        x.clear();
        y.clear();
        x.push_back(1.0);
        x.push_back(0.0);
        x.push_back(2.0);
        for (size_t i = 0; i < x.size(); ++i)
        {
            y.push_back(0.0);
        }
        unit_assert_throws_what(CubicSplineDummyInitialize(x, y), std::runtime_error, "[CubicHermiteSpline] unusable values were provided to the spline function.");

        // Should throw on non-unique x data
        x.clear();
        y.clear();
        x.push_back(1.0);
        x.push_back(2.0);
        x.push_back(2.0);
        for (size_t i = 0; i < x.size(); ++i)
        {
            y.push_back(0.0);
        }
        unit_assert_throws_what(CubicSplineDummyInitialize(x, y), std::runtime_error, "[CubicHermiteSpline] unusable values were provided to the spline function.");

        // Should throw on different lengths of x and y data
        x.clear();
        y.clear();
        x.push_back(1.0);
        x.push_back(2.0);
        x.push_back(3.0);
        for (size_t i = 0; i < x.size(); ++i)
        {
            y.push_back(0.0);
        }
        y.push_back(0.0); // add one more to y data
        unit_assert_throws_what(CubicSplineDummyInitialize(x, y), std::runtime_error, "[CubicHermiteSpline] unusable values were provided to the spline function.");

        // Should throw on different lengths of x and y data
        x.clear();
        y.clear();
        x.push_back(1.0);
        x.push_back(2.0);
        x.push_back(3.0);
        y.push_back(0.0);
        y.push_back(0.0);
        unit_assert_throws_what(CubicSplineDummyInitialize(x, y), std::runtime_error, "[CubicHermiteSpline] unusable values were provided to the spline function.");
    }

    void DiffAndIntegrateTest()
    {
        vector<double> x;
        vector<double> y;
        x.push_back(1.0);
        x.push_back(2.0);
        x.push_back(3.0);
        x.push_back(4.0);
        y.push_back(1.0);
        y.push_back(2.0);
        y.push_back(3.0);
        y.push_back(6.0);

        CubicHermiteSpline interpolator(x, y);

        unit_assert(!interpolator.IsDifferentiable());
        unit_assert(!interpolator.IsIntegrable());

        if (interpolator.IsDifferentiable())
        {
            // Differentiation is not implemented. This is a placeholder for if the feature is added.
            unit_assert(false);

            // Placeholder test of differentiation
            unit_assert_equal(interpolator.Differentiate(2.0), 1.0, 0.00001);
        }
        else
        {
            unit_assert_throws_what(interpolator.Differentiate(2.0), runtime_error, "[CubicHermiteSpline] attempted to differentiate a non-differentiable implementation of IInterpolation.")
        }

        if (interpolator.IsIntegrable())
        {
            // Integration is not implemented. This is a placeholder for if the feature is added.
            unit_assert(false);

            // Placeholder test of integration
            unit_assert_equal(interpolator.Integrate(1.0, 2.0), 0.5, 0.00001);
        }
        else
        {
            unit_assert_throws_what(interpolator.Integrate(1.0, 2.0), runtime_error, "[CubicHermiteSpline] attempted to integrate a non-integrable implementation of IInterpolation.")
        }
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        CubicHermiteSplineTest tester;
        tester.Run();
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