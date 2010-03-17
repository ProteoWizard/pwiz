//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "SavitzkyGolaySmoother.hpp"
#include "ZeroSampleFiller.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/math/round.hpp"
#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/lu.hpp>
#include <boost/utility/singleton.hpp>


namespace pwiz {
namespace analysis {


using std::min;
using std::max;


namespace {


// we generate the SG coefficients on the fly for a given window size and polynomial order
void generateSavitzkyGolayCoefficients(vector<double>& coefficients,
                                       int leftWindowSize,
                                       int rightWindowSize,
                                       int polynomialOrder)
{
    using namespace boost::numeric;

    int size = leftWindowSize + rightWindowSize + 1;
    int& order = polynomialOrder;
    int& left = leftWindowSize;
    int& right = rightWindowSize;

    vector<double>& c = coefficients;
    c.resize(size, 0.0);

    ublas::matrix<double> a(order+1, order+1);
    ublas::vector<double> b(order+1, 0.0);
    b[0] = 1.0; // 0=smooth

    // set up equations of least-squares fit;
    // loop derived from "Numerical Recipes in C: The Art of Scientific Computing", §14.8
    for (int ipj=0; ipj <= (order << 1); ++ipj)
    {
        double sum = ipj ? 0.0 : 1.0;
        for (int k=1; k <= right; ++k) sum += pow((double)k, (double)ipj);
        for (int k=1; k <= left; ++k) sum += pow((double)-k, (double)ipj);
        int mm = min(ipj, 2*order - ipj);
        for (int imj = -mm; imj <= mm; imj += 2)
            a((ipj+imj)/2, (ipj-imj)/2) = sum;
    }

    ublas::permutation_matrix<size_t> pm(order+1);
    ublas::lu_factorize(a, pm);
    ublas::lu_substitute(a, pm, b);

    // another loop derived from Numerical Recipes
    for (int k = -left; k <= right; ++k)
    {
        double sum = b[0];
        double fac = 1.0;
        for (int mm=0; mm < order; ++mm)
            sum += b[mm+1] * (fac *= k);
        c[k+left]=sum;
    }
}


/// singleton that caches the SG coefficients;
/// access is constant-time after the first time
class CoefficientCache : public boost::singleton<CoefficientCache>
{
    public:
    CoefficientCache(boost::restricted)
    {
    }

    /// If the coefficients corresponding to the requested order and width
    /// have not been cached, this calculates them.
    /// Returns the cached coefficients vector.
    const vector<double>& coefficients(int order, int width) const
    {
        size_t orderIndex = order-2; // order 2 (quadratic) is index 0
        size_t widthIndex = (width-3)/2; // width 3 is index 0, width 5 is index 1, etc.

        if (cache_.size() <= orderIndex)
            cache_.resize(orderIndex+1);

        if (cache_[orderIndex].size() <= widthIndex)
            cache_[orderIndex].resize(widthIndex+1);

        if (cache_[orderIndex][widthIndex].empty())
            generateSavitzkyGolayCoefficients(cache_[orderIndex][widthIndex],
                                              (width-1)/2,
                                              (width-1)/2,
                                              order);

        return cache_[orderIndex][widthIndex];
    }

    private:
    // cache_[order][width] = coefficients
    mutable vector< vector< vector<double> > > cache_;
};


} // namespace


struct SavitzkyGolaySmoother::Impl
{
    Impl(int order, int window)
        : order_(order), window_(window)
    {
    }

    ~Impl() {}

    int order_;
    int window_;
};


PWIZ_API_DECL
SavitzkyGolaySmoother::SavitzkyGolaySmoother(int polynomialOrder, int windowSize)
: impl_(new Impl(polynomialOrder, windowSize))
{
    if (polynomialOrder < 2 || polynomialOrder > 20)
        throw runtime_error("[SavitzkyGolaySmoother::ctor()] Invalid value for polynomial order: valid range is [2, 20]");
    if (windowSize < 5 || (windowSize % 2) == 0)
        throw runtime_error("[SavitzkyGolaySmoother::ctor()] Invalid value for window size: value must be odd and in range [5, infinity)");
    if (polynomialOrder > windowSize)
        throw runtime_error("[SavitzkyGolaySmoother::ctor()] Invalid values for polynomial order and window size: window size must be greater than polynomial order.");
}

PWIZ_API_DECL
SavitzkyGolaySmoother::~SavitzkyGolaySmoother()
{
}

PWIZ_API_DECL
void SavitzkyGolaySmoother::smooth_copy(vector<double>& x, vector<double>& y)
{
    smooth(x, y, x, y);
}

PWIZ_API_DECL
void SavitzkyGolaySmoother::smooth(const std::vector<double>& x, 
                                   const std::vector<double>& y,
                                   std::vector<double>& xSmoothed,
                                   std::vector<double>& ySmoothed)
{
    if (x.size() != y.size())
        throw runtime_error("[SavitzkyGolaySmoother::smooth()] x and y arrays must be the same size");

    vector<double>::const_iterator start;

    // the Gram polynomial coefficients to use in the SG algorithm;
    // we only use half of them because our window is symmetric
    const vector<double>& c =
        CoefficientCache::instance->coefficients(impl_->order_, impl_->window_);

    // the size of the window in either direction
    size_t flank = size_t(impl_->window_-1) / 2;

    // fill in missing samples based on window size
    // note: we don't need all the missing samples because a window full of zeros
    //       will always smooth to a 0, regardless of the X values involved
    vector<double> yCopy;
    ZeroSampleFiller::fill(x, y, xSmoothed, yCopy, flank+1);

    // calculate the smoothed y values
    ySmoothed.reserve(yCopy.size());
    ySmoothed.insert(ySmoothed.end(), yCopy.begin(), yCopy.begin()+flank);
    for (size_t i=flank, end=yCopy.size()-flank; i < end; ++i)
    {
        // start the smoothed y value with the current point
        double smoothY = c[flank] * yCopy[i];

        for (size_t offset=1; offset <= flank; ++offset)
        {
            smoothY += c[flank-offset] * yCopy[i-offset];
            smoothY += c[flank-offset] * yCopy[i+offset];
        }

        ySmoothed.push_back(max(0.0, smoothY));
    }
    ySmoothed.insert(ySmoothed.end(), yCopy.begin(), yCopy.begin()+flank);

    /*ofstream xpoints("xpoints.txt"), ypoints("ypoints.txt");
    xpoints.precision(15); ypoints.precision(15);
    for (size_t i=0; i < ySmoothed.size(); ++i)
    {
        xpoints << xSmoothed[i] << " ";
        ypoints << ySmoothed[i] << " ";
    }*/
}


} // namespace analysis
} // namespace msdata
