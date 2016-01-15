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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#include "spline.hpp"
#include <vector>
#include <boost/shared_array.hpp>


namespace freicore {

using std::vector;

struct Interpolator
{
    Interpolator(const vector<double>& x, const vector<double>& y)
    {
        _size = x.size();

        if (_size < 4)
            return;

        BOOST_ASSERT(x.size() == y.size());

        //_ypp.reset(spline_cubic_set(_size, const_cast<double*>(&x[0]), const_cast<double*>(&y[0]), 1, 0, 1, 0));

        _ypp.reset(new double[_size]);
        spline_pchip_set(_size, const_cast<double*>(&x[0]), const_cast<double*>(&y[0]), _ypp.get());
    }

    // uses interpolation on piecewise cubic splines to make an f(x) function evenly spaced on the x axis
    void resample(vector<double>& x, vector<double>& y) const
    {
        BOOST_ASSERT(_size == x.size());
        BOOST_ASSERT(_size == y.size());

        if (x.size() < 4)
            return;

        //double minSampleSize = x[1] - x[0];
        //for (int i=2; i < _size; ++i)
            //minSampleSize = min(minSampleSize, x[i] - x[i-1]);

        vector<double> sampleDeltas(_size, 0);
        for (int i=1; i < _size; ++i)
            sampleDeltas[i] = x[i] - x[i-1];
        sort(sampleDeltas.begin(), sampleDeltas.end());

        double minSampleSize = sampleDeltas[min(size_t(_size - 1), size_t(_size*0.01 + 1))];

        size_t newSize = (x.back() - x.front()) / minSampleSize;

        //double ypval, yppval;
        vector<double> newX, newY;
        newX.reserve(newSize);
        newY.reserve(newSize);
        newX.push_back(x[0]);
        newY.push_back(y[0]);
        for (size_t i=1; newX.back() < x.back(); ++i)
        {
            newX.push_back(newX.back() + minSampleSize);
            newY.push_back(0);
            int left = i-1;
            //spline_cubic_val2(_size, &x[0], newX.back(), &left, &y[0], _ypp.get(), &newY.back(), &ypval, &yppval);
            spline_pchip_val(_size, &x[0], &y[0], _ypp.get(), 1, &newX.back(), &newY.back());
        }
        swap(x, newX);
        swap(y, newY);
    }

    double interpolate(const vector<double>& xs, const vector<double>& ys, double x) const
    {
        if (x < xs.front() || x > xs.back() || xs.size() < 4)
            return 0;

        //double ypval, yppval;
        //return spline_cubic_val(_size, const_cast<double*>(&xs[0]), const_cast<double*>(&ys[0]), _ypp.get(), x, &ypval, &yppval);

        double y;
        spline_pchip_val(_size, const_cast<double*>(&xs[0]), const_cast<double*>(&ys[0]), _ypp.get(), 1, &x, &y);
        return y;
    }

    private:
    boost::shared_array<double> _ypp;
    int _size;
};

} // namespace freicore
