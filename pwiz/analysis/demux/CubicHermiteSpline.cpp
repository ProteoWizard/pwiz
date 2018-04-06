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

#include "CubicHermiteSpline.hpp"
#include "CSpline.h"
#include <boost/make_shared.hpp>

using namespace std;

namespace pwiz
{
namespace analysis
{
    CubicHermiteSpline::CubicHermiteSpline(const std::vector<double>& points, const std::vector<double>& values) :
        cSpline_(new cSpline(points, values))
    {
        CheckForError();
    }

    CubicHermiteSpline::CubicHermiteSpline(const Eigen::VectorXd& points, const Eigen::VectorXd& values)
    {
        vector<double> pointsCopied;
        vector<double> valuesCopied;
        for (int i = 0; i < points.size(); ++i)
        {
            pointsCopied.push_back(points[i]);
            valuesCopied.push_back(values[i]);
        }
        cSpline_ = new cSpline(pointsCopied, valuesCopied);
        CheckForError();
    }

    CubicHermiteSpline::~CubicHermiteSpline()
    {
        if (cSpline_ != nullptr)
            delete cSpline_;
    }

    bool CubicHermiteSpline::IsDifferentiable()
    {
        return false;
    }

    bool CubicHermiteSpline::IsIntegrable()
    {
        return false;
    }

    double CubicHermiteSpline::Differentiate(double x)
    {
        throw runtime_error("[CubicHermiteSpline] attempted to differentiate a non-differentiable implementation of IInterpolation.");
    }

    double CubicHermiteSpline::Integrate(double a, double b)
    {
        throw runtime_error("[CubicHermiteSpline] attempted to integrate a non-integrable implementation of IInterpolation.");
    }

    double CubicHermiteSpline::Interpolate(double x)
    {
        return cSpline_->getY(x);
    }

    void CubicHermiteSpline::CheckForError() const
    {
        if (cSpline_->IsError())
            throw runtime_error("[CubicHermiteSpline] unusable values were provided to the spline function.");
    }
} // namespace analysis
} // namespace pwiz